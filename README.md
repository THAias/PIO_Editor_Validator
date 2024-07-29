## Introduction ##
This is a "Validator Service" (derived from [Firely's official FHIR validator API][validator-docu]) for validating
FHIR XML data according to the PIO-ULB specification. [PIO-ULB][pio-üb] (german shortcut) is a "Nursing Information
Object" which contains relevant patient data for the Transfer Process (patient transfer from facility A to facility B).
All relevant [FHIR profile][fhir-profiles] information are already integrated in the validator. This software
was developed within the [Care Regio][care-regio] research project as part of the PIO-ULB-Editor.

## Release notes ##
This is the first and only release.

## Documentation ##
The subproject "Firely.Fhir.Validation.R4" in folder "src" contains the main logic to validate PIO-ULBs. This subproject
holds the FHIR structure definitions which are used as validation source files. These source files were
downloaded from [simplifier.net][pio-üb]. Some corrections were necessary. These corrections are automated by class
"ProcessValidationSource" and already done in advance. There is no need to run the correction process again.

The class "PIOEditorValidator" in the same subProject is the main class of the validator service. This class sets up the
validation and filters some validation results. It is necessary to filter some errors and warnings due to three reasons:
- The validator service is part of the PIO-ULB-Editor which just supports a subset of the whole FHIR specification (we call it PIO-Small). Due to the reduction some errors and warnings appear.
- Mistakes in the specification source files (= FHIR structure definitions)
- Firely Validator mistakes

There is a second relevant subproject "WebAPI" in the "src" folder. This sub project provides an API to receive xml
data (route: http://localhost:5212/validate). The xml string must be added to the body of the http request. After
successful validation a http response with the validation result is sent back. Always wait for the response before
starting a new validation process.  

## Getting Started ##
To start the API of the validation service, you need to run the "WebAPI" sub project.
Therefore, compile the whole project and run the "Programm.cs" file in the sub project "WebAPI".

## Copyright ##
Copyright (c) 2013-2024, HL7, Firely (info@fire.ly), Microsoft Open Technologies
and contributors. See the file CONTRIBUTORS for details

[validator-docu]: https://docs.fire.ly/projects/Firely-NET-SDK/en/latest/validation/profile-validation.html#
[pio-üb]: https://simplifier.net/ulb
[fhir-spec]: http://www.hl7.org/fhir
[fhir-profiles]: https://hl7.org/FHIR/profiling.html
[care-regio]: https://care-regio.de/