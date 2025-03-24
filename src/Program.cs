using System;
using System.Runtime.CompilerServices;
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
    private static readonly Dictionary<string, string> _fhirServers = new()
    {
        { "PublicFirelyServer", "https://server.fire.ly/r4" },
        { "PublicHapi", "http://hapi.fhir.org/baseR4/"},
        { "Local", "http://localhost:8080/fhirR4"}
    };

    private static readonly string _fhirServerUrl = _fhirServers["PublicFirelyServer"];

    /// <summary>
    /// Main method.
    /// </summary>
    /// <param name="args"></param>
    /// <returns></returns>
    static async Task<int> Main(string[] args)
    {
        try
        {
            FhirClientSettings fhirClientSettings = new()
            {
                PreferredFormat = ResourceFormat.Json,
                ReturnPreference = ReturnPreference.Representation,
                UseAsync = true
            };

            FhirClient client = new(_fhirServerUrl, fhirClientSettings);

            bool createPatient = false;
            if (createPatient)
            {
                var createdPatient = await CreatePatient(
                    client, ["Coody"], "IO", new Date(1990, 1, 1));
            }

            var patients = await GetPatientsAsync(client, ["name=Coody"]);

            bool deletePatients = false;
            if (deletePatients)
            {
                Console.WriteLine($"Number of patients before deleting: {patients.Count}");
                string firstPatientId = string.Empty;

                foreach (var element in patients)
                {
                    if (string.IsNullOrEmpty(firstPatientId))
                    {
                        firstPatientId = element.Id;
                        continue;
                    }

                    await DeletePatient(client, element);
                }
            }

            if (patients.Count > 0)
            {
                var patient = await GetPatientAsync(client, patients[0].Id);
                Console.WriteLine("Patient before update:");
                PrintPatient(patient);

                if (patient != null)
                {
                    var updatedPatient = await UpdatePatient(client, patient);
                    Console.WriteLine("Patient after update:");
                    PrintPatient(updatedPatient);
                }
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    /// <summary>
    /// Get a patient.
    /// </summary>
    /// <param name="client"></param>
    /// <param name="patientId"></param>
    /// <returns></returns>
    static async Task<Patient?> GetPatientAsync(
        FhirClient client,
        string patientId)
    {
        Console.WriteLine($"Getting patient {patientId}");

        Patient? patient = await client.ReadAsync<Patient>($"Patient/{patientId}");

        return patient;
    }


    /// <summary>
    /// Get patients with encounters.
    /// </summary>
    /// <param name="client"> the FHIR client to fetch the patients from</param>
    /// <param name="searchParams">The criteria for searching the patients. (Default: null) </param>
    /// <param name="maxNumberOfPatients">The maximum amount of patients to return. (Default: 5)</param>
    /// <param name="onlyWithEncounters">Whether to only return patients with encounters. (Default: false)</param>
    /// <returns></returns>
    static async Task<List<Patient>> GetPatientsAsync(
        FhirClient client,
        string[]? searchParams = null,
        int maxNumberOfPatients = 5,
        bool onlyWithEncounters = false)
    {
        Bundle? patientBundle = await client.SearchAsync<Patient>(searchParams);

        if (patientBundle?.Total != null)
        {
            Console.WriteLine($"Total number of patients: {patientBundle?.Total}");
        }

        List<Patient> patients = [];

        while (patientBundle != null)
        {
            Console.WriteLine($"Entry count: {patientBundle?.Entry.Count}");

#pragma warning disable CS8602 // Dereference of a possibly null reference.
            foreach (Bundle.EntryComponent entry in patientBundle.Entry)
#pragma warning restore CS8602 // Dereference of a possibly null reference.
            {
                if (entry.Resource is Patient patient)
                {
                    Bundle? patientsEncounterBundle = await client.SearchAsync<Encounter>([$"patient=Patient/{patient.Id}"]);

                    if (onlyWithEncounters
                        && (patientsEncounterBundle == null || patientsEncounterBundle.Total == 0))
                    {
                        continue;
                    }

                    patients.Add(patient);

                    Console.WriteLine($"- {patients.Count}: {entry.FullUrl}");

                    if (patients.Count >= maxNumberOfPatients)
                    {
                        break;
                    }
                }
            }

            if (patients.Count >= maxNumberOfPatients)
            {
                break;
            }

            if (patientBundle.NextLink == null)
            {
                break;
            }

            patientBundle = await client.ContinueAsync(patientBundle);
        }

        return patients;
    }

    /// <summary>
    /// Create a patient.
    /// </summary>
    /// <param name="client"></param>
    /// <param name="givenNames"></param>
    /// <param name="familyName"></param>
    /// <param name="birthDate"></param>
    /// <returns></returns>
    static async Task<Patient?> CreatePatient(
        FhirClient client,
        string[] givenNames,
        string familyName,
        Date birthDate)
    {
        Console.WriteLine("Creating patient");

        Patient patientToCreate = new()
        {
            Name =
            [
                new HumanName
                {
                    Family = familyName,
                    Given = givenNames
                }
            ],
            BirthDateElement = birthDate
        };

        Patient? createdPatient = await client.CreateAsync(patientToCreate);

        Console.WriteLine($"Created patient: {createdPatient?.Name[0].Given.First()}" +
         $"{createdPatient?.Name[0].Family}");

        return createdPatient;
    }

    static async Task<Patient?> UpdatePatient(
        FhirClient client,
        Patient patient)
    {
        Console.WriteLine($"Updating patient {patient.Id}");

        patient.Telecom.Add(
            new ContactPoint(ContactPoint.ContactPointSystem.Phone,
                             ContactPoint.ContactPointUse.Mobile,
                             "1234567890"));

        patient.Gender = AdministrativeGender.Unknown;
        return await client.UpdateAsync(patient);
    }

    /// <summary>
    /// Delete a patient.
    /// </summary>
    /// <param name="client"></param>
    /// <param name="patient"></param>
    /// <returns></returns>
    static async System.Threading.Tasks.Task DeletePatient(
        FhirClient client,
        Patient patient)
    {
        Console.WriteLine($"Deleting patient {patient.Id}");
        await client.DeleteAsync($"Patient/{patient.Id}?_cascade=delete");
    }

    /// <summary>
    /// Print a patient.
    /// </summary>
    /// <param name="patient"></param>
    static void PrintPatient(Patient? patient)
    {
        if (patient == null)
        {
            return;
        }

        string patientInfo = $"Patient: {patient.Id}";

        if (patient.Name.Count > 0)
        {
            patientInfo += $" Name: {patient.Name[0].Given.First()} {patient.Name[0].Family}";
        }

        if (patient.BirthDateElement != null)
        {
            patientInfo += $" Birthdate: {patient.BirthDateElement}";
        }

        if (patient.Gender != null)
        {
            patientInfo += $" Gender: {patient.Gender}";
        }

        if (patient.Telecom.Count > 0)
        {
            patientInfo += $" System: {patient.Telecom[0].System} Use: {patient.Telecom[0].Use} Value: {patient.Telecom[0].Value}";
        }

        Console.WriteLine(patientInfo);
    }
}