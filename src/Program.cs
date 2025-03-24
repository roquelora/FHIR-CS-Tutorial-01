using System;
using System.Threading.Tasks;
using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using Hl7.Fhir.Serialization;
using Hl7.Fhir.Utility;

namespace fhir_cs_tutorial_01;

/// <summary>
/// Main program class.
/// </summary>
public static class Program
{
    private const string _fhirServerUrl = "https://server.fire.ly/r4";

    static async System.Threading.Tasks.Task Main(string[] args)
    {
        FhirClientSettings fhirClientSettings = new()
        {
            PreferredFormat = ResourceFormat.Json,
            ReturnPreference = ReturnPreference.Representation
        };

        FhirClient client = new(_fhirServerUrl, fhirClientSettings);

        Console.WriteLine("Hello, World!");

        string[] searchParams = ["name=mark"];

        Bundle? patientBundle = await client.SearchAsync<Patient>(searchParams);

        int numberOfPatientsWithEncounters = 0;

        List<string> patientsWithEncounters = [];
        int maxNumberOfPatientsWithEncounters = 2;

        while (patientBundle != null)
        {
            Console.WriteLine($"Total number of patients: {patientBundle?.Total} - Entry count: {patientBundle?.Entry.Count}");

            foreach (Bundle.EntryComponent entry in patientBundle?.Entry)
            {
                if (entry.Resource is Patient patient)
                {
                    Bundle? patientsEncounterBundle = await client.SearchAsync<Encounter>([$"patient=Patient/{patient.Id}"]);

                    if (patientsEncounterBundle == null || patientsEncounterBundle.Total == 0)
                    {
                        continue;
                    }

                    Console.WriteLine($" - Id: {patient.Id}");

                    if (patient.Name.Count > 0)
                    {
                        Console.WriteLine($" - Name: {patient.Name[0]}");
                    }

                    patientsWithEncounters.Add(patient.Id);

                    Console.WriteLine($"- Patient # with encounters {numberOfPatientsWithEncounters,3}: {entry.FullUrl}");

                    numberOfPatientsWithEncounters++;

                    if (patientsWithEncounters.Count >= maxNumberOfPatientsWithEncounters)
                    {
                        break;
                    }
                }
            }

            if (patientsWithEncounters.Count >= maxNumberOfPatientsWithEncounters)
            {
                break;
            }

            if (patientBundle.NextLink == null)
            {
                break;
            }

            patientBundle = await client.ContinueAsync(patientBundle);
        }


    }
}