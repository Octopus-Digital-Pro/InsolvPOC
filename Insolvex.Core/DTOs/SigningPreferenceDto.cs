namespace Insolvex.Core.DTOs;

public record SigningPreferenceDto(bool UseSavedSigningKey);

public record UpdateSigningPreferenceRequest(bool UseSavedSigningKey);
