using RemSolution.Application.Common.Documents;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Entities;
using RemSolution.Domain.Enums;

namespace RemSolution.Application.Features.DocumentTemplate.Commands
{
    /// <summary>One placeholder binding as submitted by the editor.</summary>
    public record DocumentTemplateFieldInput
    {
        public string Placeholder { get; init; } = string.Empty;
        public DocumentFieldBinding Binding { get; init; }
        public string? DataPath { get; init; }
        public string? FixedValue { get; init; }
        public string? Label { get; init; }
        public bool IsRequired { get; init; }
    }

    /// <summary>
    /// The writable shape of a template, shared by create and update so both apply
    /// the same rules and neither can drift.
    /// </summary>
    public interface IDocumentTemplatePayload
    {
        string Name { get; }
        DocumentTemplateKind Kind { get; }
        string Language { get; }
        List<DocumentBlock> Blocks { get; }
        List<DocumentTemplateFieldInput>? Fields { get; }
    }

    public abstract class DocumentTemplatePayloadValidator<T> : AbstractValidator<T>
        where T : IDocumentTemplatePayload
    {
        protected DocumentTemplatePayloadValidator(ILocalizer localizer)
        {
            RuleFor(v => v.Name)
                .NotEmpty().WithMessage(_ => localizer["Validation.DocumentTemplate.NameRequired"])
                .MaximumLength(200);

            RuleFor(v => v.Kind).IsInEnum();

            RuleFor(v => v.Language)
                .Must(Languages.IsSupported)
                    .WithMessage(_ => localizer["Validation.DocumentTemplate.LanguageUnsupported"]);

            RuleFor(v => v.Blocks)
                .NotEmpty().WithMessage(_ => localizer["Validation.DocumentTemplate.BlocksRequired"]);

            RuleForEach(v => v.Fields)
                .ChildRules(field =>
                {
                    field.RuleFor(f => f.Placeholder).NotEmpty().MaximumLength(120);
                    field.RuleFor(f => f.Binding).IsInEnum();
                    field.RuleFor(f => f.FixedValue).MaximumLength(1000);
                    field.RuleFor(f => f.Label).MaximumLength(200);
                })
                .When(v => v.Fields is not null);

            // A data-field binding that points at a path the resolver does not
            // answer would print blank on every document ever generated from this
            // template — worth failing loudly at save time instead.
            RuleForEach(v => v.Fields)
                .Must((command, field) => field.Binding != DocumentFieldBinding.DataField
                                          || DocumentPlaceholders.IsAvailableFor(
                                              field.DataPath ?? field.Placeholder, command.Kind))
                .WithMessage((_, field) =>
                    localizer["Validation.DocumentTemplate.UnknownDataPath", field.DataPath ?? field.Placeholder])
                .When(v => v.Fields is not null);
        }
    }

    /// <summary>
    /// Turns the submitted payload into the rows to persist, applying the
    /// auto-binding rule for placeholders the editor did not mention.
    /// </summary>
    public static class DocumentTemplatePayloadMapper
    {
        public static List<DocumentTemplateField> ToFields(IDocumentTemplatePayload payload) =>
            DocumentTemplateFields.Reconcile(
                payload.Blocks,
                (payload.Fields ?? new List<DocumentTemplateFieldInput>()).Select(input => new DocumentTemplateField
                {
                    Placeholder = input.Placeholder,
                    Binding = input.Binding,
                    // Defaulting the path to the placeholder name keeps the common
                    // case ({{client.cin}} bound to client.cin) out of the payload.
                    DataPath = input.Binding == DocumentFieldBinding.DataField
                        ? input.DataPath ?? input.Placeholder
                        : null,
                    FixedValue = input.Binding == DocumentFieldBinding.FixedValue ? input.FixedValue : null,
                    Label = input.Binding == DocumentFieldBinding.AskEachTime ? input.Label : null,
                    IsRequired = input.Binding == DocumentFieldBinding.AskEachTime && input.IsRequired
                }),
                payload.Kind);
    }
}
