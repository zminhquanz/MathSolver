using MathSolver.Models;

namespace MathSolver.Services.QuestionBank;

/// <summary>Companion animals and ornamental fish with countable, welfare-aware group sizes.</summary>
internal sealed record PetAnimalSubject(QuestionUnit Unit, string ViVenue, string EnVenue,
    int Capacity, int MaxFactor, int MaxPerGroup);

internal static class PetAnimalThemeCatalogue
{
    // Companion animals and aquarium species; avoid wild predators and livestock.
    // Background references:
    // https://www.akc.org/dog-breeds/pomeranian/
    // https://www.rspca.org.uk/adviceandwelfare/pets/rodents/hamsters
    // https://www.rspca.org.uk/adviceandwelfare/pets/birds/environment
    // https://ornamentalfish.org/what-we-do/advice-information/care-sheets/caresheets-tropical-freshwater-fish/how-to-look-after-guppies-and-mollies/
    // https://www.aucklandzoo.co.nz/animals/asian-arowana
    // Arowanas and flowerhorns are specifically requested aquarium exceptions;
    // each aquarium is limited to one fish in the generated arithmetic scenes.
    internal static IReadOnlyList<PetAnimalSubject> Subjects { get; } = QuizContentCatalog.LoadList<PetAnimalSubject>("PetAnimalThemeSubjects");

    internal static PetAnimalSubject? Find(string unitId) => Subjects.FirstOrDefault(s => s.Unit.Id == unitId);
    internal static PetAnimalSubject? FindScene(string sceneId) =>
        sceneId.StartsWith("theme-animal-", StringComparison.Ordinal)
            ? Subjects.FirstOrDefault(s => sceneId.StartsWith("theme-animal-" + s.Unit.Id + "-", StringComparison.Ordinal))
            : null;
    internal static string Group(PetAnimalSubject subject, AppLanguage language) =>
        subject.Unit.Group(language, singular: true);
}
