using RemSolution.Application.Common.Documents;
using RemSolution.Application.Common.Exceptions;
using RemSolution.Application.Features.Contract.Commands.GenerateContractCommand;
using RemSolution.Application.Features.DocumentTemplate.Commands;
using RemSolution.Application.Features.DocumentTemplate.Commands.CreateDocumentTemplateCommand;
using RemSolution.Application.Features.DocumentTemplate.Commands.SetDefaultDocumentTemplateCommand;
using RemSolution.Application.Features.DocumentTemplate.Commands.SetDocumentTemplateActiveCommand;
using RemSolution.Application.Features.DocumentTemplate.Commands.UpdateDocumentTemplateCommand;
using RemSolution.Application.Features.DocumentTemplate.Queries.GetDocumentPromptQuery;
using RemSolution.Application.Features.DocumentTemplate.Queries.GetDocumentTemplateByIdQuery;
using RemSolution.Application.Features.DocumentTemplate.Queries.GetDocumentTemplatesQuery;
using RemSolution.Application.Features.Facture.Commands.GenerateFactureCommand;
using RemSolution.Application.Features.Renting.Commands.CreateRentingCommand;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Entities;
using RemSolution.Domain.Enums;
using RemSolution.Domain.ValueObjects;
using TemplateEntity = RemSolution.Domain.Entities.DocumentTemplate;

namespace RemSolution.Application.FunctionalTests.Documents;

using static Testing;

/// <summary>
/// Document layouts: who may manage them, how the one to use is chosen, and what
/// happens to a template that asks the agent for something.
/// </summary>
public class DocumentTemplateTests : BaseTestFixture
{
    private static readonly DateTime Start = new(2030, 6, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime End = new(2030, 6, 4, 0, 0, 0, DateTimeKind.Utc);

    [Test]
    public async Task ShouldCreateATemplateAndMakeTheFirstOneTheDefault()
    {
        await RunAsAgencyAdministratorAsync();
        var agencyId = await AddTestAgencyAsync();

        var id = await SendAsync(ContractTemplate("Our contract"));

        var template = await FindAsync<TemplateEntity>(id);
        template.Should().NotBeNull();
        template!.AgencyId.Should().Be(agencyId);
        template.IsActive.Should().BeTrue();
        template.IsDefault.Should().BeTrue(
            "the first template of a kind and language must become the default, or generation would keep using the shipped example");
    }

    [Test]
    public async Task ShouldNotMakeASecondTemplateTheDefault()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        await SendAsync(ContractTemplate("First"));
        var secondId = await SendAsync(ContractTemplate("Second"));

        (await FindAsync<TemplateEntity>(secondId))!.IsDefault.Should().BeFalse();
    }

    /// <summary>
    /// The auto-binding rule: a placeholder named after a booking value is mapped
    /// for you, anything else becomes something to ask the agent.
    /// </summary>
    [Test]
    public async Task ShouldAutoBindRecognisedPlaceholdersAndAskAboutTheRest()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        var id = await SendAsync(ContractTemplate("Auto",
            "Renter {{client.fullName}} accepts a franchise of {{franchise}}."));

        var template = await SendAsync(new GetDocumentTemplateByIdQuery(id));

        var known = template!.Fields.Single(f => f.Placeholder == DocumentPlaceholders.ClientFullName);
        known.Binding.Should().Be(DocumentFieldBinding.DataField);
        known.DataPath.Should().Be(DocumentPlaceholders.ClientFullName);

        var unknown = template.Fields.Single(f => f.Placeholder == "franchise");
        unknown.Binding.Should().Be(DocumentFieldBinding.AskEachTime);
    }

    /// <summary>
    /// A data-field binding pointed at a path the resolver cannot answer would print
    /// blank on every document ever generated — rejected at save time instead.
    /// </summary>
    [Test]
    public async Task ShouldRejectABindingToAnUnknownBookingField()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        var command = ContractTemplate("Bad", "{{whatever}}") with
        {
            Fields = new List<DocumentTemplateFieldInput>
            {
                new()
                {
                    Placeholder = "whatever",
                    Binding = DocumentFieldBinding.DataField,
                    DataPath = "client.notARealField"
                }
            }
        };

        await FluentActions.Invoking(() => SendAsync(command)).Should().ThrowAsync<ValidationException>();
    }

    /// <summary>
    /// Invoice totals do not exist on a contract, so binding one there is refused
    /// rather than silently printing nothing.
    /// </summary>
    [Test]
    public async Task ShouldRejectAnInvoiceOnlyFieldOnAContractTemplate()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        var command = ContractTemplate("Bad", "{{total}}") with
        {
            Fields = new List<DocumentTemplateFieldInput>
            {
                new()
                {
                    Placeholder = "total",
                    Binding = DocumentFieldBinding.DataField,
                    DataPath = DocumentPlaceholders.FactureTotal
                }
            }
        };

        await FluentActions.Invoking(() => SendAsync(command)).Should().ThrowAsync<ValidationException>();

        // The same binding on an invoice template is fine.
        var invoice = ContractTemplate("Good", "{{total}}") with
        {
            Kind = DocumentTemplateKind.Facture,
            Fields = new List<DocumentTemplateFieldInput>
            {
                new()
                {
                    Placeholder = "total",
                    Binding = DocumentFieldBinding.DataField,
                    DataPath = DocumentPlaceholders.FactureTotal
                }
            }
        };

        (await SendAsync(invoice)).Should().BeGreaterThan(0);
    }

    [Test]
    public async Task ShouldRequireAName()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        await FluentActions.Invoking(() => SendAsync(ContractTemplate(string.Empty)))
            .Should().ThrowAsync<ValidationException>();
    }

    [Test]
    public async Task ShouldRequireAtLeastOneBlock()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        var command = ContractTemplate("Empty") with { Blocks = new List<DocumentBlock>() };

        await FluentActions.Invoking(() => SendAsync(command)).Should().ThrowAsync<ValidationException>();
    }

    // ---- which template gets used ----

    [Test]
    public async Task ShouldUseTheAgencyDefaultWhenNoTemplateIsChosen()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        var templateId = await SendAsync(ContractTemplate("Our contract"));
        var rentingId = await SeedRentingAsync();

        var contract = await SendAsync(new GenerateContractCommand { RentingId = rentingId });

        var stored = await FindAsync<Contract>(contract.Id);
        stored!.DocumentTemplateId.Should().Be(templateId);
        stored.TemplateName.Should().Be("Our contract");
    }

    /// <summary>
    /// An agency that has never opened the template screen still gets paperwork: the
    /// shipped example, recorded as "no template row".
    /// </summary>
    [Test]
    public async Task ShouldFallBackToTheShippedExampleWithNoTemplateOfItsOwn()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        var rentingId = await SeedRentingAsync();
        var contract = await SendAsync(new GenerateContractCommand { RentingId = rentingId });

        var stored = await FindAsync<Contract>(contract.Id);
        stored!.DocumentTemplateId.Should().BeNull();
        stored.TemplateName.Should().NotBeNullOrWhiteSpace("the example's name is still recorded");
    }

    [Test]
    public async Task ShouldUseAnExplicitlyChosenTemplate()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        await SendAsync(ContractTemplate("Default one"));
        var chosenId = await SendAsync(ContractTemplate("Long stay"));

        var rentingId = await SeedRentingAsync();

        var contract = await SendAsync(new GenerateContractCommand
        {
            RentingId = rentingId,
            TemplateId = chosenId
        });

        (await FindAsync<Contract>(contract.Id))!.TemplateName.Should().Be("Long stay");
    }

    [Test]
    public async Task ShouldRejectAnUnknownTemplate()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        var rentingId = await SeedRentingAsync();

        await FluentActions.Invoking(() => SendAsync(new GenerateContractCommand
        {
            RentingId = rentingId,
            TemplateId = 9999
        })).Should().ThrowAsync<ValidationException>();
    }

    /// <summary>
    /// Templates are tenant data: another agency's id is not found, not borrowed.
    /// </summary>
    [Test]
    public async Task ShouldNotUseAnotherAgencysTemplate()
    {
        await RunAsAgencyAdministratorAsync();

        await AddTestAgencyAsync();
        var otherAgencyTemplate = await SendAsync(ContractTemplate("Theirs"));

        await AddTestAgencyAsync();
        var rentingId = await SeedRentingAsync("TPL-B");

        await FluentActions.Invoking(() => SendAsync(new GenerateContractCommand
        {
            RentingId = rentingId,
            TemplateId = otherAgencyTemplate
        })).Should().ThrowAsync<ValidationException>();
    }

    [Test]
    public async Task ShouldSwapTheDefault()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        var firstId = await SendAsync(ContractTemplate("First"));
        var secondId = await SendAsync(ContractTemplate("Second"));

        await SendAsync(new SetDefaultDocumentTemplateCommand(secondId));

        (await FindAsync<TemplateEntity>(firstId))!.IsDefault.Should().BeFalse();
        (await FindAsync<TemplateEntity>(secondId))!.IsDefault.Should().BeTrue();
    }

    /// <summary>
    /// Retiring the default gives up the default too: a retired template is skipped
    /// by generation, so leaving it marked would strand the agency on a layout that
    /// silently never applies.
    /// </summary>
    [Test]
    public async Task ShouldGiveUpTheDefaultWhenRetired()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        var id = await SendAsync(ContractTemplate("Retiring"));

        await SendAsync(new SetDocumentTemplateActiveCommand(id, false));

        var template = await FindAsync<TemplateEntity>(id);
        template!.IsActive.Should().BeFalse();
        template.IsDefault.Should().BeFalse();

        // And generation falls back to the shipped example rather than failing.
        var rentingId = await SeedRentingAsync();
        var contract = await SendAsync(new GenerateContractCommand { RentingId = rentingId });

        (await FindAsync<Contract>(contract.Id))!.DocumentTemplateId.Should().BeNull();
    }

    /// <summary>
    /// Retiring must actually stop the layout being used, not merely hide it from the
    /// pickers — otherwise passing the id explicitly is a way around the admin's
    /// decision, and the retired wording keeps reaching customers.
    /// </summary>
    [Test]
    public async Task ShouldRefuseARetiredTemplateEvenWhenChosenExplicitly()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        var templateId = await SendAsync(ContractTemplate("Retiring"));
        await SendAsync(new SetDocumentTemplateActiveCommand(templateId, false));

        var rentingId = await SeedRentingAsync();

        await FluentActions.Invoking(() => SendAsync(new GenerateContractCommand
        {
            RentingId = rentingId,
            TemplateId = templateId
        })).Should().ThrowAsync<ValidationException>();

        (await CountAsync<Contract>()).Should().Be(0);
    }

    [Test]
    public async Task ShouldHideRetiredTemplatesFromThePickerButNotFromTheAdminList()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        var id = await SendAsync(ContractTemplate("Retiring"));
        await SendAsync(new SetDocumentTemplateActiveCommand(id, false));

        (await SendAsync(new GetDocumentTemplatesQuery())).Should().BeEmpty();
        (await SendAsync(new GetDocumentTemplatesQuery { IncludeInactive = true })).Should().HaveCount(1);
    }

    // ---- ask-each-time fields ----

    [Test]
    public async Task ShouldReportWhatTheAgentMustBeAskedFor()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        var templateId = await SendAsync(ContractTemplate(
            "With franchise", "Franchise of {{franchise}} applies to {{client.fullName}}."));

        var prompts = await SendAsync(new GetDocumentPromptQuery(DocumentTemplateKind.Contract, templateId));

        prompts.Should().ContainSingle()
            .Which.Placeholder.Should().Be("franchise", "the recognised field needs no prompt");
    }

    [Test]
    public async Task ShouldAskForNothingWithTheShippedExample()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        (await SendAsync(new GetDocumentPromptQuery(DocumentTemplateKind.Contract))).Should().BeEmpty();
    }

    [Test]
    public async Task ShouldGenerateWithASuppliedManualValue()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        var templateId = await SendAsync(RequiredFranchiseTemplate());
        var rentingId = await SeedRentingAsync();

        var contract = await SendAsync(new GenerateContractCommand
        {
            RentingId = rentingId,
            TemplateId = templateId,
            ManualValues = new Dictionary<string, string> { ["franchise"] = "500 TND" }
        });

        contract.Number.Should().NotBeNullOrWhiteSpace();
    }

    /// <summary>
    /// A required value the agent did not supply must fail the generation, not print
    /// a contract with a hole where the franchise should be.
    /// </summary>
    [Test]
    public async Task ShouldRefuseToGenerateWithARequiredValueMissing()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        var templateId = await SendAsync(RequiredFranchiseTemplate());
        var rentingId = await SeedRentingAsync();

        await FluentActions.Invoking(() => SendAsync(new GenerateContractCommand
        {
            RentingId = rentingId,
            TemplateId = templateId
        })).Should().ThrowAsync<ValidationException>();

        (await CountAsync<Contract>()).Should().Be(0, "nothing may be recorded for a refused generation");
    }

    /// <summary>
    /// The same rule on the booking screen's on-save path: the whole save rolls back,
    /// rather than leaving a renting whose promised contract never appeared.
    /// </summary>
    [Test]
    public async Task ShouldRollBackTheRentingWhenARequiredDocumentValueIsMissing()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        var templateId = await SendAsync(RequiredFranchiseTemplate());
        var carId = await SeedBookableCarAsync("TPL-SAVE");
        var clientId = await SeedClientAsync();

        await FluentActions.Invoking(() => SendAsync(new CreateRentingCommand
        {
            CarId = carId,
            ClientId = clientId,
            StartDate = Start,
            EndDate = End,
            GenerateContract = true,
            ContractTemplateId = templateId
        })).Should().ThrowAsync<ValidationException>();

        (await CountAsync<Renting>()).Should().Be(0);
        (await CountAsync<Contract>()).Should().Be(0);
    }

    [Test]
    public async Task ShouldGenerateOnSaveWithTheValuesTheAgentSupplied()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        var templateId = await SendAsync(RequiredFranchiseTemplate());
        var carId = await SeedBookableCarAsync("TPL-SAVE-OK");
        var clientId = await SeedClientAsync();

        var rentingId = await SendAsync(new CreateRentingCommand
        {
            CarId = carId,
            ClientId = clientId,
            StartDate = Start,
            EndDate = End,
            GenerateContract = true,
            ContractTemplateId = templateId,
            DocumentValues = new Dictionary<string, string> { ["franchise"] = "500 TND" }
        });

        (await FindAsync<Renting>(rentingId)).Should().NotBeNull();
        (await CountAsync<Contract>()).Should().Be(1);
    }

    // ---- who may manage layouts ----

    /// <summary>
    /// Layouts are configuration, like the reference catalogs: an administrator's
    /// job, not a staff permission.
    /// </summary>
    [Test]
    public async Task ShouldRefuseTemplateManagementToStaff()
    {
        await RunAsAgencyStaffAsync(Permissions.RentingCreate, Permissions.ContractGenerate);
        await AddTestAgencyAsync();

        await FluentActions.Invoking(() => SendAsync(ContractTemplate("Staff attempt")))
            .Should().ThrowAsync<ForbiddenAccessException>();
    }

    /// <summary>
    /// Staff still READ the list: choosing which contract to print is part of the
    /// generate flow, not administration.
    /// </summary>
    [Test]
    public async Task ShouldLetStaffListTemplates()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();
        await SendAsync(ContractTemplate("Ours"));

        await RunAsAgencyStaffAsync(Permissions.RentingRead);

        (await SendAsync(new GetDocumentTemplatesQuery())).Should().HaveCount(1);
    }

    /// <summary>
    /// The feature gate follows the template's KIND, which is why it is checked in the
    /// handler rather than declared on the request.
    /// </summary>
    [Test]
    public async Task ShouldGateTemplateManagementOnTheMatchingFeature()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();
        await AddAsync(new AgencyFeature { Feature = FeatureFlags.Contracts, Enabled = false });

        await FluentActions.Invoking(() => SendAsync(ContractTemplate("Blocked")))
            .Should().ThrowAsync<ForbiddenAccessException>();

        // Invoices are a separate module and are untouched.
        var invoice = ContractTemplate("Allowed") with { Kind = DocumentTemplateKind.Facture };
        (await SendAsync(invoice)).Should().BeGreaterThan(0);
    }

    [Test]
    public async Task ShouldUpdateBlocksAndRebindPlaceholders()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        var id = await SendAsync(ContractTemplate("Editable", "Hello {{client.fullName}}."));
        var loaded = await SendAsync(new GetDocumentTemplateByIdQuery(id));

        await SendAsync(new UpdateDocumentTemplateCommand
        {
            Id = id,
            RowVersion = loaded!.RowVersion,
            Name = "Edited",
            Kind = DocumentTemplateKind.Contract,
            Language = Languages.French,
            Blocks = Blocks("Hello {{client.fullName}}, franchise {{franchise}}.")
        });

        var updated = await SendAsync(new GetDocumentTemplateByIdQuery(id));

        updated!.Name.Should().Be("Edited");
        updated.Fields.Should().Contain(f => f.Placeholder == "franchise",
            "a placeholder added by the edit gets a binding of its own");
    }

    // ---- helpers ----

    private static CreateDocumentTemplateCommand ContractTemplate(
        string name, string paragraph = "A standard clause.") => new()
    {
        Name = name,
        Kind = DocumentTemplateKind.Contract,
        Language = Languages.French,
        Blocks = Blocks(paragraph)
    };

    private static CreateDocumentTemplateCommand RequiredFranchiseTemplate() =>
        ContractTemplate("Franchise required", "Franchise of {{franchise}}.") with
        {
            Fields = new List<DocumentTemplateFieldInput>
            {
                new()
                {
                    Placeholder = "franchise",
                    Binding = DocumentFieldBinding.AskEachTime,
                    Label = "Franchise",
                    IsRequired = true
                }
            }
        };

    private static List<DocumentBlock> Blocks(string paragraph) => new()
    {
        new DocumentBlock { Type = DocumentBlockType.Heading, Text = "CONTRAT" },
        new DocumentBlock { Type = DocumentBlockType.Paragraph, Text = paragraph }
    };

    private static async Task<int> SeedRentingAsync(string matricule = "TPL-1")
    {
        var carId = await SeedBookableCarAsync(matricule);
        var clientId = await SeedClientAsync();

        return await SendAsync(new CreateRentingCommand
        {
            CarId = carId, ClientId = clientId, StartDate = Start, EndDate = End
        });
    }

    private static async Task<int> SeedBookableCarAsync(string matricule)
    {
        var car = new Car
        {
            Matricule = matricule,
            Status = CarStatus.Active,
            DailyRate = Money.Of(50m, "TND"),
        };
        await AddAsync(car);
        return car.Id;
    }

    private static async Task<int> SeedClientAsync()
    {
        var client = new Client
        {
            FirstName = "Template",
            LastName = "Client",
            BirthDate = new DateTime(1990, 1, 1)
        };
        await AddAsync(client);
        return client.Id;
    }
}
