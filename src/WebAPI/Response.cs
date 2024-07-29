using Firely.Fhir.Validation.R4;

namespace WebAPI;

/// <summary>Interface for API response.</summary>
public class Response
{
    /// <summary>True, if API request was successful</summary>
    public bool success { get; set; }

    /// <summary>Response message</summary>
    public string message { get; set; }
    
    /// <summary>Data of the API response (= validation result)</summary>
    public ValidationResult? data { get; set; }
}