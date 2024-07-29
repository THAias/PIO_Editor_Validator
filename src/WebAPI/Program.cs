using Firely.Fhir.Validation.R4;
using WebAPI;

var builder = WebApplication.CreateBuilder(args);

//Allow requests from frontend (http://localhost:3000)
string MyAllowSpecificOrigins = "_myAllowSpecificOrigins";
builder.Services.AddCors(options =>
{
    options.AddPolicy(name: MyAllowSpecificOrigins, policy =>
    {
        string[] whitelist = [
            "http://localhost:3000",
            "http://localhost",
            "https://pio-editor.de",
            "https://pio-editor.de/api",
            "https://pio-editor.de/editor",
            "https://www.pio-editor.de",
            "https://www.pio-editor.de/api",
            "https://www.pio-editor.de/editor"
        ];
        policy.WithOrigins(whitelist).AllowAnyHeader().AllowAnyMethod();
    });
});

//Get WebApplication instance and do configurations
var app = builder.Build();
app.UseHttpsRedirection();
app.UseCors(MyAllowSpecificOrigins);

//This route will trigger the validation process of an pio xml string send in http request body
app.MapPost("/validate", async (HttpRequest request) =>
{
    Console.WriteLine("New validation process started ...");
    string xmlString = "";

    try
    {
        //Reading xml data from http request body
        using (StreamReader stream = new StreamReader(request.Body))
        {
            xmlString = await stream.ReadToEndAsync();
        }
    }
    catch (Exception e)
    {
        string message = "Reading http request body failed due to following error: " + e.Message;
        Console.WriteLine("ERROR: " + message);
        return Results.Ok(new Response {
            success = false,
            message = message
        });
    }
    
    try
    {
        ValidationResult validationResult = PIOEditorValidator.validate(xmlString, true);
        return Results.Ok(new Response {
            success = true,
            message = "Successful validation",
            data = validationResult
        });
    }
    catch (Exception e)
    {
        string message = "Validation failed due to following error: " + e.Message;
        Console.WriteLine("ERROR: " + message);
        return Results.Ok(new Response {
            success = false,
            message = message
        });
    }
});

app.Run();
