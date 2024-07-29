using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace Firely.Fhir.Validation.R4;

/// <summary>
/// Class for processing all xml validation sources, like structure definitions, code systems and value sets. This
/// process corrects mistakes in the source files and is necessary to avoid errors during offline validation. 
/// </summary>
public class ProcessValidationSource
{
    /// <summary>This Method starts the file processing.</summary>
    /// <param name="pathToFiles">Absolute or relative path to the validation files</param>
    /// <param name="overwriteFiles">If true, validation files will be directly overwritten. If false, a new directory
    /// "processedFiles" will be generated</param>
    /// 
    public static void processFiles(string pathToFiles, bool overwriteFiles = true)
    {
        Console.WriteLine("--------------- REMOVING VERSION ANNOTATIONS FROM URLS ---------------");
        string[] affectedXmlTags = ["url", "baseDefinition", "targetProfile", "profile", "valueSet", "patternCanonical"];
        removeVersionAnnotationsFromUrls(pathToFiles, affectedXmlTags, overwriteFiles);
    }
    
    /// <summary>Removes all version annotation from all 'relevantNodes' in structureDefinitions, code systems and value
    /// sets (e.g. "KBV_PR_MIO_ULB_Patient|1.4.0" is changed to "KBV_PR_MIO_ULB_Patient")</summary>
    /// <param name="pathToFiles">Absolute or relative path to the validation files</param>
    /// <param name="relevantXmlNodes">Name of the xml nodes, where version annotations could be found</param>
    /// <param name="overwriteFiles">If true, validation files will be directly overwritten. If false, a new directory
    /// "processedFiles" will be generated</param>
    /// <param name="skipOutputFilesWhichAlreadyExist">If true, all files, which are already existing in output
    /// directory "processedFiles", are skipped</param>
    private static void removeVersionAnnotationsFromUrls(string pathToFiles, string[] relevantXmlNodes, bool overwriteFiles, bool skipOutputFilesWhichAlreadyExist = true)
    {
        //Declare variables
        string outputPath = Path.Combine(pathToFiles, "processedFiles");
        string[] allFilePaths = Directory.GetFiles(pathToFiles);
        int changedFilesCounter = 0;
        int totalChangesCounter = 0;
        bool fileChanged = false;
        List<string> changesInFile = new();
        XDocument doc;
        
        //Create output directory
        if (!overwriteFiles)
        {
            Directory.CreateDirectory(outputPath);
            if (!skipOutputFilesWhichAlreadyExist) foreach (FileInfo file in new DirectoryInfo(outputPath).GetFiles()) file.Delete();
        }
        
        //Iterate through all files
        foreach (string path in allFilePaths)
        {
            doc = XDocument.Load(path, LoadOptions.SetLineInfo);
            string fileName = Path.GetFileName(path);
            
            //Skip, if output xml file already exists in directory "processedFiles"
            if (!overwriteFiles && skipOutputFilesWhichAlreadyExist && File.Exists(Path.Combine(outputPath, fileName)))
            {
                Console.WriteLine("SKIPPED: File " + fileName + " already exists");
                continue;
            }
            
            //Iterate through every single xml node
            foreach (XElement element in doc.Descendants())
            {
                string nodeName = element.Name.ToString().Split("}")[1];
                XAttribute? att = element.FirstAttribute;
                
                if (relevantXmlNodes.Contains(nodeName) && att != null)
                {
                    //Get relevant data
                    IXmlLineInfo info = element;
                    int line = info.LineNumber + 1;
                    string oldValue = att.Value;
                    string newValue = att.Value;
                    
                    //Delete version from url ("KBV_PR_MIO_ULB_Patient|1.4.0" is changed to "KBV_PR_MIO_ULB_Patient")
                    if (oldValue.Contains("|"))
                    {
                        string firstPart = oldValue.Split("|")[0];
                        string secondPart = oldValue.Split("|")[1];
                        if (!secondPart.Contains("/")) newValue = firstPart;
                    }
                    
                    //Write new value to xml document
                    if (oldValue != newValue)
                    {
                        att.Value = newValue;
                        changesInFile.Add("Line: " + line + ", <" + nodeName + ">   ->   " + oldValue + " CHANGED TO " + newValue);
                        fileChanged = true;
                    }
                }
                
                //Save changes to xml file. This task sometimes fails randomly -> Try task in loop
                if (fileChanged)
                {
                    bool success = false;
                    int waitAfterFailToWriteData = 10000; //ms
                    while (!success)
                    {
                        try
                        {
                            doc.Save(overwriteFiles ? pathToFiles : Path.Combine(outputPath, fileName));
                            success = true;
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine("File " + fileName + " could not be saved due to following error: " + e.Message);
                            Console.WriteLine("Programm will wait for " + (waitAfterFailToWriteData / 1000) + " seconds and try saving again ...");
                            System.Threading.Thread.Sleep(waitAfterFailToWriteData);
                        }
                    }
                }
            }
            
            //Update filesChanged counter and totalChanges counter
            totalChangesCounter += changesInFile.Count;
            if (fileChanged) changedFilesCounter++;

            //Console output
            Console.WriteLine(changesInFile.Count + " changes in file " + fileName);
            foreach (string change in changesInFile)
            {
                Console.WriteLine("    " + change);
            }
            
            //Reset variables
            changesInFile.Clear();
            fileChanged = false;
        }
        
        //Console output
        Console.WriteLine("Removing version annotations successfully finished (" + totalChangesCounter + " total changes in " + changedFilesCounter + " files)");
        if (changedFilesCounter > 0)
        {
            if (overwriteFiles) Console.WriteLine("Files in directory " + pathToFiles + " were directly overwritten!");
            else Console.WriteLine("All files with changes are saved to new directory " + outputPath);
        }
        
    }
}