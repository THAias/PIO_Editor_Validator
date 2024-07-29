using Firely.Fhir.Packages;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using Hl7.Fhir.Specification;
using Hl7.Fhir.Specification.Source;
using Hl7.Fhir.Specification.Terminology;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using static Firely.Fhir.Validation.R4.ProcessValidationSource;
using Formatting = Newtonsoft.Json.Formatting;

namespace Firely.Fhir.Validation.R4;

/// <summary>Interface with validation result information.</summary>
public class ValidationResult
{
    /// <summary>The overall result like "failure" or "success"</summary>
    public string overallResult { get; set; } = null!;
    
    /// <summary>Number of errors detected</summary>
    public int numberOfErrors { get; set; }
    
    /// <summary>Number of warnings detected</summary>
    public int numberOfWarnings { get; set; }
    
    /// <summary>Number of errors detected</summary>
    public int totalFilteredErrors { get; set; }
    
    /// <summary>Number of warnings detected</summary>
    public int totalFilteredWarnings { get; set; }
    
    /// <summary>All errors as string array</summary>
    public string[] errors { get; set; } = null!;

    /// <summary>All warnings as string array</summary>
    public string[] warnings { get; set; } = null!;
    
    /// <summary>All filtered errors related to pio small reduction</summary>
    public string[]? filteredErrorsPioSmallRelated { get; set; }
    
    /// <summary>All filtered errors related to specification</summary>
    public string[]? filteredErrorsSpecificationRelated { get; set; }
    
    /// <summary>All filtered errors related to validator mistakes</summary>
    public string[]? filteredErrorsValidatorRelated { get; set; }
    
    /// <summary>All filtered warnings related to pio small reduction</summary>
    public string[]? filteredWarningsPioSmallRelated { get; set; }
    
    /// <summary>All filtered warnings related to specification</summary>
    public string[]? filteredWarningsSpecificationRelated { get; set; }
    
    /// <summary>All filtered warnings related to validator mistakes</summary>
    public string[]? filteredWarningsValidatorRelated { get; set; }
}

/// <summary>
/// Class for validating a PIO-ÜB xml file. The validation sources are offline structure definitions located in
/// "/structureDefinitions" directory. The validation result gets filtered because some errors and warnings can be lead
/// back to the PIO Small reduction.
/// </summary>
public class PIOEditorValidator
{
    static void Main(string[] args)
    {
        //Set parameter
        bool processValidationFiles = false;
        string pathToXmlFile = Path.GetFullPath(Path.Combine(".." ,".." ,".." , "testPIO", "PrinzFinnVonStaufenPIO.xml"));
        string pathToValidationFiles = Path.GetFullPath(Path.Combine("structureDefinitions"));
        
        //Correct validation files if needed and validate xml pio
        if (processValidationFiles) processFiles(pathToValidationFiles, false);
        ValidationResult result = validate(pathToXmlFile, false);
        
        Console.WriteLine("\n" + Newtonsoft.Json.JsonConvert.SerializeObject(result, Formatting.Indented));
    }

    /// <summary>Validates a PIO xml file</summary>
    /// <param name="xmlString">Xml string representing the whole pio</param>
    /// <param name="calledFromAPI">Must be true, if method is called from api project, which is a separate project with
    /// different absolute paths</param>
    /// <returns>The filtered validation result</returns>
    public static ValidationResult validate(string xmlString, bool calledFromAPI)
    {
        //Setup validator
        string directoryToStructureDefinitions;
        if (calledFromAPI) directoryToStructureDefinitions = Path.GetFullPath(Path.Combine("..", "Firely.Fhir.Validation.R4", "structureDefinitions"));
        else directoryToStructureDefinitions = Path.GetFullPath(Path.Combine("..", "..", "..", "structureDefinitions"));
        const string packageServerUrl = "https://packages.simplifier.net";
        var fhirRelease = FhirRelease.R4;
        var packageSource = FhirPackageSource.CreateCorePackageSource(ModelInfo.ModelInspector, fhirRelease, packageServerUrl);
        var filesResolver = new DirectorySource(directoryToStructureDefinitions);
        var combinedSource = new MultiResolver(filesResolver, packageSource);
        var profileSource = new CachedResolver(combinedSource);
        var terminologySource = new LocalTerminologyService(profileSource);
        var validationSettings = new ValidationSettings();
        var validator = new Validator(profileSource, terminologySource, null, validationSettings);

        //Parse xml data to internal object
        XmlReader xmlReader = XmlReader.Create(new StringReader(xmlString));
        var deserSettings = new FhirXmlPocoDeserializerSettings { ValidateOnFailedParse = true };
        var xmlPocoDeserializer = new FhirXmlPocoDeserializer(deserSettings);
        Resource? output;
        IEnumerable<Hl7.Fhir.Utility.CodedException> issues;
        bool successFlag = xmlPocoDeserializer.TryDeserializeResource(xmlReader, out output, out issues);
        
        //Create result directory
        string resultDirectory;
        if (calledFromAPI) resultDirectory = Path.Combine("..","Firely.Fhir.Validation.R4", "results");
        else resultDirectory = Path.Combine("..", "..", "..", "results");
        Directory.CreateDirectory(resultDirectory);
        
        //Process parsing issues
        string issuesOutputPath;
        int numberOfIssues = 0;
        if (calledFromAPI) issuesOutputPath = Path.Combine("..","Firely.Fhir.Validation.R4", "results", "parsingIssues.txt");
        else issuesOutputPath = Path.Combine("..", "..", "..", "results", "parsingIssues.txt");
        if (!successFlag)
        {
            //Write issues to file
            using (StreamWriter outputFile = new StreamWriter(issuesOutputPath))
            {
                outputFile.WriteLine(Newtonsoft.Json.JsonConvert.SerializeObject(issues, Formatting.Indented));
            }
            
            //Console output
            numberOfIssues = issues.Count();
        }
        else
        {
            if (File.Exists(issuesOutputPath)) File.Delete(issuesOutputPath);
            numberOfIssues = -1;
        }

        //Validate parsed object by using structure definitions as validation source
        if (output != null)
        {
            //Validate parsed data (can take up to 30 seconds)
            OperationOutcome result = validator.Validate(output);
            
            //Write all validation results to file
            ValidationResult unfilteredResult = getValidationResultObject(result.ToString(), false);
            string unfilteredOutputPath;
            if (calledFromAPI) unfilteredOutputPath = Path.Combine("..","Firely.Fhir.Validation.R4", "results", "allValidationResult.txt");
            else unfilteredOutputPath = Path.Combine("..", "..", "..", "results", "allValidationResult.txt");
            using (StreamWriter outputFile = new(unfilteredOutputPath))
            {
                outputFile.WriteLine(Newtonsoft.Json.JsonConvert.SerializeObject(unfilteredResult, Formatting.Indented));
            }
            
            //Write filtered validation results to file
            ValidationResult filteredResult = getValidationResultObject(result.ToString(), true);
            string filteredOutputPath;
            if (calledFromAPI) filteredOutputPath = Path.Combine("..","Firely.Fhir.Validation.R4", "results", "filteredValidationResult.txt");
            else filteredOutputPath = Path.Combine("..", "..", "..", "results", "filteredValidationResult.txt");
            using (StreamWriter outputFile = new(filteredOutputPath))
            {
                outputFile.WriteLine(Newtonsoft.Json.JsonConvert.SerializeObject(filteredResult, Formatting.Indented));
            }
            
            Console.WriteLine("-------------------------------");
            Console.WriteLine("VALIDATION RESULT: " + filteredResult.overallResult);
            Console.WriteLine(unfilteredResult.numberOfErrors + " errors and " + unfilteredResult.numberOfWarnings + " warnings before filtering");
            Console.WriteLine(filteredResult.numberOfErrors + " errors and " + filteredResult.numberOfWarnings + " warnings after filtering");
            Console.WriteLine("INFO: All unfiltered errors and warnings are listed in file " + Path.GetFullPath(filteredOutputPath));
            if (numberOfIssues > 0)
            {
                Console.WriteLine("WARNING: " + numberOfIssues + " Parsing issues detected");
                if (numberOfIssues > 50) Console.WriteLine("INFO: A high number of issues can be caused by a wrong order of xml tags. The parser generates issues for every xml tag, which is in wrong order.");
                Console.WriteLine("INFO: All parsing issues are listed in file " + Path.GetFullPath(issuesOutputPath));
            }
            Console.WriteLine("Validation of xml file SUCCESSFULLY completed");
            Console.WriteLine("-------------------------------");
            return filteredResult;
        }

        throw new Exception("Parsing of xml data failed. Parsed object is null");
    }

    /// <summary>Generates a validation result object</summary>
    /// <param name="result">Unformatted validation result string</param>
    /// <param name="filterResults">If true, specific errors and warnings will be filtered, because of PIO Small
    /// reduction</param>
    /// <returns>An result object where errors and warnings are stored under separate keys</returns>
    private static ValidationResult getValidationResultObject(string result, bool filterResults)
    {
        //Split the result string into single parts
        string[] temp = result.Split("\n");
        
        //Extract the overall result information
        string overallResult = temp[0].Split(" (")[0].Replace("Overall result: ", "");
        List<string> temp2 = temp.ToList();
        temp2.RemoveAt(0);
        temp2.RemoveAt(temp2.Count - 1);
        
        //Separate errors and warnings
        List<string> errors = new List<string>();
        List<string> warnings = new List<string>();
        foreach (string element in temp2)
        {
            if (element.Contains("[ERROR]")) errors.Add(element.Replace("\r", ""));
            if (element.Contains("[WARNING]")) warnings.Add(element.Replace("\r", ""));
        }

        //Generate return object (could be filtered)
        if (filterResults)
        {
            List<string> pioSmallRelatedErrors = new List<string>();
            List<string> specificationRelatedErrors = new List<string>();
            List<string> validatorRelatedErrors = new List<string>();
            List<string> pioSmallRelatedWarnings = new List<string>();
            List<string> specificationRelatedWarnings = new List<string>();
            List<string> validatorRelatedWarnings = new List<string>();
            string[] filteredErrors = (from error in errors where !filterSingelError(error, ref pioSmallRelatedErrors, ref specificationRelatedErrors, ref validatorRelatedErrors) select error).ToArray();
            string[] filteredWarnings = (from warning in warnings where !filterSingelWarning(warning, ref pioSmallRelatedWarnings, ref specificationRelatedWarnings, ref validatorRelatedWarnings) select warning).ToArray();
            return new ValidationResult
            {
                overallResult = errors.Count > 0 && filteredErrors.Length == 0 ? "SUCCESS" : overallResult,
                numberOfErrors = filteredErrors.Length,
                numberOfWarnings = filteredWarnings.Length,
                totalFilteredErrors = pioSmallRelatedErrors.Count + specificationRelatedErrors.Count + validatorRelatedErrors.Count,
                totalFilteredWarnings = pioSmallRelatedWarnings.Count + specificationRelatedWarnings.Count + validatorRelatedWarnings.Count,
                errors = filteredErrors,
                warnings = filteredWarnings,
                filteredErrorsPioSmallRelated = pioSmallRelatedErrors.ToArray(),
                filteredErrorsSpecificationRelated = specificationRelatedErrors.ToArray(),
                filteredErrorsValidatorRelated = validatorRelatedErrors.ToArray(),
                filteredWarningsPioSmallRelated = pioSmallRelatedWarnings.ToArray(),
                filteredWarningsSpecificationRelated = specificationRelatedWarnings.ToArray(),
                filteredWarningsValidatorRelated = validatorRelatedWarnings.ToArray()
            };
        }

        return new ValidationResult
        {
            overallResult = overallResult, numberOfErrors = errors.Count, numberOfWarnings = warnings.Count,
            errors = errors.ToArray(), warnings = warnings.ToArray()
        };

    }
    
    /// <summary>This method will check whether the warning string should be filtered.</summary>
    /// <param name="warning">A single validation warning</param>
    /// <param name="pioSmallRelated">Reference to List for storing pio small related warnings</param>
    /// <param name="specificationRelated">Reference to List for storing specification related warnings</param>
    /// <param name="validatorRelated">Reference to List for storing validator related warnings</param>
    /// <returns>True, if the warning should be filtered</returns>
    private static bool filterSingelWarning(string warning, ref List<string> pioSmallRelated, ref List<string> specificationRelated, ref List<string> validatorRelated)
    {
        if (isWarningSpecificationRelated(warning))
        {
            specificationRelated.Add(warning);
            return true;
        }

        if (isWarningPioSmallRelated(warning))
        {
            pioSmallRelated.Add(warning);
            return true;
        }

        if (isWarningValidatorRelated(warning))
        {
            validatorRelated.Add(warning);
            return true;
        }

        return false;
    }
    
    /// <summary>This method will check whether the error string should be filtered.</summary>
    /// <param name="error">A single validation error</param>
    /// <param name="pioSmallRelated">Reference to List for storing pio small related errors</param>
    /// <param name="specificationRelated">Reference to List for storing specification related errors</param>
    /// <param name="validatorRelated">Reference to List for storing validator related errors</param>
    /// <returns>True, if the error should be filtered</returns>
    private static bool filterSingelError(string error, ref List<string> pioSmallRelated, ref List<string> specificationRelated, ref List<string> validatorRelated)
    {
        if (isErrorSpecificationRelated(error))
        {
            specificationRelated.Add(error);
            return true;
        }

        if (isErrorPioSmallRelated(error))
        {
            pioSmallRelated.Add(error);
            return true;
        }

        if (isErrorValidatorRelated(error))
        {
            validatorRelated.Add(error);
            return true;
        }

        return false;
    }

    /// <summary>This method will check whether the error string should be filtered due to pio small reduction issues.</summary>
    /// <param name="error">A single validation error</param>
    /// <returns>True, if the error should be filtered</returns>
    private static bool isErrorPioSmallRelated(string error)
    {
        return (error.Contains("KBV_PR_MIO_ULB_Device") && Regex.Match(error, @"Instance count is 0, which is not within the specified cardinality of 1..1 \(at Bundle.entry\[[0-9]+\].resource\[[0-9]+\].deviceName\[[0-9]+\].type").Success) ||
               (error.Contains("KBV_PR_MIO_ULB_Observation_Total_Barthel_Index") && Regex.Match(error, @"Instance count is 0, which is not within the specified cardinality of 10..10 \(at Bundle.entry\[[0-9]+\].resource\[[0-9]+\].hasMember").Success) ||
               (error.Contains("https://fhir.kbv.de/StructureDefinition/KBV_PR_MIO_ULB_AllergyIntolerance") && Regex.Match(error, "AllergyIntolerance.clinicalStatus SHALL be present if verificationStatus is not entered-in-error").Success) ||
               (error.Contains("https://fhir.kbv.de/StructureDefinition/KBV_PR_MIO_ULB_DiagnosticReport_Vital_Signs_and_Body_Measures).result") && Regex.Match(error, @"Instance count is 0, which is not within the specified cardinality of 1..1 \(at Bundle.entry\[[0-9]+\].resource\[[0-9]+\].effective\[x\]").Success) ||
               (error.Contains("KBV_PR_MIO_ULB_Patient") && Regex.Match(error, @"Instance count is 0, which is not within the specified cardinality of 1..1 \(at Bundle.entry\[[0-9]+\].resource\[[0-9]+\].identifier\[[0-9]+\].system").Success);
    }
    
    /// <summary>This method will check whether the error string should be filtered due to specification or validation
    /// file issues.</summary>
    /// <param name="error">A single validation error</param>
    /// <returns>True, if the error should be filtered</returns>
    private static bool isErrorSpecificationRelated(string error)
    {
        return (error.Contains("Value does not match pattern") && Regex.Match(error, @"meta\[[0-9]+\].profile\[[0-9]+\]").Success) ||
               error.Contains("The Coding references a value set, not a code system") ||
               error.Contains("Value does not match pattern '{") && error.Contains("system") ||
               error.Contains("Referenced resource '' does not validate against any of the expected target profiles") ||
               (error.Contains("KBV_PR_MIO_ULB_Observation_Degree_Of_Disability") && Regex.Match(error, @"Instance count is [0-9]+, which is not within the specified cardinality of 0..1 \(at Bundle.entry\[[0-9]+\].resource\[[0-9]+\].component").Success) ||
               (error.Contains("https://fhir.kbv.de/StructureDefinition/KBV_PR_MIO_ULB_DiagnosticReport_Vital_Signs_and_Body_Measures") && error.Contains("https://fhir.kbv.de/StructureDefinition/KBV_PR_MIO_ULB_Observation_Assessment_Free") && Regex.Match(error, @"Instance count is [0-9]+, which is not within the specified cardinality of 1..1 \(at Bundle.entry\[[0-9]+\].resource\[0\].category").Success) ||
               (error.Contains("https://fhir.kbv.de/StructureDefinition/KBV_PR_MIO_ULB_DiagnosticReport_Vital_Signs_and_Body_Measures") && error.Contains("https://fhir.kbv.de/StructureDefinition/KBV_PR_MIO_ULB_Observation_Assessment_Free") && Regex.Match(error, @"Instance count is [0-9]+, which is not within the specified cardinality of 0..0 \(at Bundle.entry\[[0-9]+\].resource\[0\].note").Success) ||
               (error.Contains("https://fhir.kbv.de/StructureDefinition/KBV_PR_MIO_ULB_DiagnosticReport_Vital_Signs_and_Body_Measures") && Regex.Match(error, @"Value does not match pattern '2.72 \(at Bundle.entry\[[0-9]+\].resource\[0\].code\[0\].coding\[[0-9]+\].version\[0\]").Success) ||
               (error.Contains("Value does not match pattern") && error.Contains("https://fhir.kbv.de/StructureDefinition/KBV_PR_MIO_ULB_DiagnosticReport_Vital_Signs_and_Body_Measures") && Regex.Match(error, @"at Bundle.entry\[[0-9]+\].resource\[0\].valueQuantity\[0\].unit\[0\]").Success) ||
               (error.Contains("https://fhir.kbv.de/StructureDefinition/KBV_PR_MIO_ULB_DiagnosticReport_Vital_Signs_and_Body_Measures") && error.Contains("https://fhir.kbv.de/StructureDefinition/KBV_PR_MIO_ULB_Observation_Assessment_Free") && Regex.Match(error, @"Instance count is 1, which is not within the specified cardinality of 0..0 \(at Bundle.entry\[[0-9]+\].resource\[0\].method").Success) ||
               (error.Contains("https://fhir.kbv.de/StructureDefinition/KBV_PR_MIO_ULB_Observation_Body_Height") && Regex.Match(error, "from system 'http://unitsofmeasure.org' does not exist in the value set 'VitalSignDE_Body_Length_UCUM'").Success) ||
               (error.Contains("https://fhir.kbv.de/StructureDefinition/KBV_PR_MIO_ULB_Observation_Body_Weight") && Regex.Match(error, "from system 'http://unitsofmeasure.org' does not exist in the value set 'VitalSignDE_Body_Weigth_UCUM'").Success) ||
               (error.Contains("https://fhir.kbv.de/StructureDefinition/KBV_PR_MIO_ULB_Observation_Blood_Pressure") && Regex.Match(error, "from system 'http://unitsofmeasure.org' does not exist in the value set 'UCUM Vitals Common DE'").Success) ||
               (error.Contains("https://fhir.kbv.de/StructureDefinition/KBV_PR_MIO_ULB_Device_Aid") && Regex.Match(error, @"Instance count is 0, which is not within the specified cardinality of 1..1 \(at Bundle.entry\[[0-9]+\].resource\[0\].extension").Success) ||
               (error.Contains("https://fhir.kbv.de/StructureDefinition/KBV_PR_MIO_ULB_AllergyIntolerance") && error.Contains("Instance failed constraint ext-1") && error.Contains("Must have either extensions or value[x], not both") && Regex.Match(error, @"at Bundle.entry\[[0-9]+\].resource\[0\].extension\[[0-9]+\]").Success) ||
               (error.Contains("https://fhir.kbv.de/StructureDefinition/KBV_PR_MIO_ULB_Observation_Presence_Functional_Assessment") && error.Contains("https://fhir.kbv.de/StructureDefinition/KBV_PR_MIO_ULB_Observation_Assessment_Free") && Regex.Match(error, @"Instance count is [0-9]+, which is not within the specified cardinality of 1..1 \(at Bundle.entry\[[0-9]+\].resource\[[0-9]+\].category").Success) ||
               (error.Contains("https://fhir.kbv.de/StructureDefinition/KBV_PR_MIO_ULB_Observation_Presence_Functional_Assessment") && error.Contains("https://fhir.kbv.de/StructureDefinition/KBV_PR_MIO_ULB_Observation_Assessment_Free") && Regex.Match(error, @"Instance count is [0-9]+, which is not within the specified cardinality of 1..1 \(at Bundle.entry\[[0-9]+\].resource\[[0-9]+\].valueQuantity\[[0-9]+\].unit").Success);
    }
    
    /// <summary>This method will check whether the error string should be filtered due to validator mistakes</summary>
    /// <param name="error">A single validation error</param>
    /// <returns>True, if the error should be filtered</returns>
    private static bool isErrorValidatorRelated(string error)
    {
        return (error.Contains("KBV_PR_MIO_ULB_Condition_Care_Problem") && Regex.Match(error, @"Instance count is [0-9]+, which is not within the specified cardinality of 0..0 \(at Bundle.entry\[[0-9]+\].resource\[[0-9]+\].clinicalStatus").Success) ||
               (error.Contains("KBV_PR_MIO_ULB_Condition_Care_Problem") && Regex.Match(error, @"Instance count is [0-9]+, which is not within the specified cardinality of 0..0 \(at Bundle.entry\[[0-9]+\].resource\[[0-9]+\].verificationStatus").Success) ||
               (error.Contains("KBV_PR_MIO_ULB_Condition_Care_Problem") && Regex.Match(error, @"Instance count is [0-9]+, which is not within the specified cardinality of 1..1 \(at Bundle.entry\[[0-9]+\].resource\[[0-9]+\].category").Success) ||
               (error.Contains("KBV_PR_MIO_ULB_Condition_Care_Problem") && Regex.Match(error, @"Instance count is [0-9]+, which is not within the specified cardinality of 0..0 \(at Bundle.entry\[[0-9]+\].resource\[[0-9]+\].severity").Success) ||
               (error.Contains("KBV_PR_MIO_ULB_Condition_Care_Problem") && Regex.Match(error, @"Instance count is [0-9]+, which is not within the specified cardinality of 0..0 \(at Bundle.entry\[[0-9]+\].resource\[[0-9]+\].abatementDateTime").Success) ||
               (error.Contains("KBV_PR_MIO_ULB_Condition_Care_Problem") && Regex.Match(error, @"Instance count is [0-9]+, which is not within the specified cardinality of 0..0 \(at Bundle.entry\[[0-9]+\].resource\[[0-9]+\].asserter").Success) ||
               (error.Contains("KBV_PR_MIO_ULB_Condition_Medical_Problem_Diagnosis") && Regex.Match(error, @"Instance count is [0-9]+, which is not within the specified cardinality of 0..0 \(at Bundle.entry\[[0-9]+\].resource\[[0-9]+\].category").Success) ||
               (error.Contains("https://fhir.kbv.de/StructureDefinition/KBV_PR_MIO_ULB_Observation_Body_Weight") && Regex.Match(error, @"Instance count is 0, which is not within the specified cardinality of 1..1 \(at Bundle.entry\[[0-9]+\].resource\[[0-9]+\].value\[x\]").Success) ||
               (error.Contains("https://fhir.kbv.de/StructureDefinition/KBV_PR_MIO_ULB_Observation_Body_Weight") && Regex.Match(error, @"Instance count is [0-9]+, which is not within the specified cardinality of 0..0 \(at Bundle.entry\[[0-9]+\].resource\[[0-9]+\].component").Success) ||
               (error.Contains("https://fhir.kbv.de/StructureDefinition/KBV_PR_MIO_ULB_Observation_Body_Weight") && Regex.Match(error, @"Instance count is 0, which is not within the specified cardinality of 1..1 \(at Bundle.entry\[[0-9]+\].resource\[0\].code\[0\].coding").Success) ||
               (error.Contains("https://fhir.kbv.de/StructureDefinition/KBV_PR_MIO_ULB_Observation_Blood_Pressure") && Regex.Match(error, @"Instance count is 0, which is not within the specified cardinality of 1..1 \(at Bundle.entry\[[0-9]+\].resource\[0\].code\[[0-9]+\].coding").Success) ||
               (error.Contains("https://fhir.kbv.de/StructureDefinition/KBV_PR_MIO_ULB_Observation_Blood_Pressure") && Regex.Match(error, @"Instance count is 0, which is not within the specified cardinality of 2..* \(at Bundle.entry\[[0-9]+\].resource\[0\].component").Success) ||
               (error.Contains("https://fhir.kbv.de/StructureDefinition/KBV_PR_MIO_ULB_Observation_Peripheral_Oxygen_Saturation") && Regex.Match(error, @"Instance count is 0, which is not within the specified cardinality of 1..1 \(at Bundle.entry\[[0-9]+\].resource\[0\].method").Success) ||
               (error.Contains("https://fhir.kbv.de/StructureDefinition/KBV_PR_MIO_ULB_DiagnosticReport_Vital_Signs_and_Body_Measures") && Regex.Match(error, @"Instance count is 1, which is not within the specified cardinality of 0..0 \(at Bundle.entry\[[0-9]+\].resource\[0\].valueQuantity").Success) ||
               (error.Contains("https://fhir.kbv.de/StructureDefinition/KBV_PR_MIO_ULB_Observation_Glucose_Concentration") && Regex.Match(error, "does not exist in the value set 'Blutzucker Einheit'").Success) ||
               (error.Contains("https://fhir.kbv.de/StructureDefinition/KBV_PR_MIO_ULB_Observation_Body_Height") && Regex.Match(error, @"Value does not match pattern 'cm \(at Bundle.entry\[[0-9]+\].resource\[0\].valueQuantity\[[0-9]+\].code\[0\]").Success) ||
               (error.Contains("https://fhir.kbv.de/StructureDefinition/KBV_PR_MIO_ULB_Observation_Personal_Statements") && Regex.Match(error, "from system 'http://snomed.info/sct' does not exist in the value set 'Information Persönliche Erklärung'").Success) ||
               (error.Contains("https://fhir.kbv.de/StructureDefinition/KBV_PR_MIO_ULB_Device") && Regex.Match(error, @"Instance count is 1, which is not within the specified cardinality of 0..0 \(at Bundle.entry\[[0-9]+\].resource\[0\].note").Success) ||
               (error.Contains("https://fhir.kbv.de/StructureDefinition/KBV_PR_MIO_ULB_Device_Other_Item") && Regex.Match(error, @"Instance count is 1, which is not within the specified cardinality of 0..0 \(at Bundle.entry\[[0-9]+\].resource\[0\].type\[[0-9]+\].coding").Success) ||
               (error.Contains("https://fhir.kbv.de/StructureDefinition/KBV_PR_MIO_ULB_Device_Other_Item") && Regex.Match(error, @"Instance count is 0, which is not within the specified cardinality of 1..1 \(at Bundle.entry\[[0-9]+\].resource\[0\].type\[[0-9]+\].text").Success) ||
               (error.Contains("https://fhir.kbv.de/StructureDefinition/KBV_PR_MIO_ULB_Device_Aid") && Regex.Match(error, @"Instance count is [0-9]+, which is not within the specified cardinality of [0-9]..[0-9] \(at Bundle.entry\[[0-9]+\].resource\[0\].(udiCarrier|serialNumber|deviceName|modelNumber)").Success) ||
               (error.Contains("https://fhir.kbv.de/StructureDefinition/KBV_PR_MIO_ULB_Device_Aid") && Regex.Match(error, @"Instance count is 0, which is not within the specified cardinality of 1..* \(at Bundle.entry\[[0-9]+\].resource\[0\].extension").Success) ||
               (error.Contains("https://fhir.kbv.de/StructureDefinition/KBV_PR_MIO_ULB_Composition).section[orientierungPsyche]") && Regex.Match(error, @"Instance count is [0-9]+, which is not within the specified cardinality of [0-9]..[0-9] \(at Bundle.entry\[[0-9]+\].resource\[0\].(valueCodeableConcept|component|category|effective\[x\]|value\[x\]|)").Success) ||
               (error.Contains("KBV_PR_MIO_ULB_Observation_Total_Barthel_Index") && Regex.Match(error, @"Instance count is [0-9]+, which is not within the specified cardinality of 1..1 \(at Bundle.entry\[[0-9]+\].resource\[[0-9]+\].code\[[0-9]+\].coding").Success) ||
               (error.Contains("KBV_PR_MIO_ULB_Observation_Total_Barthel_Index") && Regex.Match(error, @"Element does not match any slice and the group is closed. \(at Bundle.entry\[[0-9]+\].resource\[[0-9]+\].code\[[0-9]+\].coding\[[0-9]+\]").Success);
    }
    
    /// <summary>This method will check whether the warning string should be filtered due to pio small reduction issues.</summary>
    /// <param name="warning">A single validation warning</param>
    /// <returns>True, if the warning should be filtered</returns>
    private static bool isWarningPioSmallRelated(string warning)
    {
        return (warning.Contains("Wenn die Extension 'Precinct' (Stadtteil) verwendet wird, dann muss diese Information auch als separates line-item abgebildet sein.") && Regex.Match(warning, @"at Bundle.entry\[[0-9]+\].resource\[0\].address\[[0-9]+\]").Success);
    }
    
    /// <summary>This method will check whether the warning string should be filtered due to specification or validation
    /// file issues.</summary>
    /// <param name="warning">A single validation warning</param>
    /// <returns>True, if the warning should be filtered</returns>
    private static bool isWarningSpecificationRelated(string warning)
    {
        return (warning.Contains("Condition.clinicalStatus SHALL be present if verificationStatus is not entered-in-error and category is problem-list-item") && warning.Contains("https://fhir.kbv.de/StructureDefinition/KBV_PR_MIO_ULB_Condition_Care_Problem")) ||
               (warning.Contains("Terminology service failed while validating concept with coding(s)") && warning.Contains("valueset 'http://fhir.de/ValueSet/merkzeichen-de' is unknown")) ||
               (warning.Contains("Terminology service failed while validating code") && warning.Contains("cannot find codesystem 'urn:ietf:bcp:13'"));
    }
    
    /// <summary>This method will check whether the warning string should be filtered due to validator mistakes</summary>
    /// <param name="warning">A single validation warning</param>
    /// <returns>True, if the warning should be filtered</returns>
    private static bool isWarningValidatorRelated(string warning)
    {
        return false;
    }
}
