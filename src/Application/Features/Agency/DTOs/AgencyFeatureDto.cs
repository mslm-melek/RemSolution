namespace RemSolution.Application.Features.Agency.DTOs;

/// <summary>One feature module and whether it is enabled for an agency.</summary>
public record AgencyFeatureDto(string Feature, bool Enabled);
