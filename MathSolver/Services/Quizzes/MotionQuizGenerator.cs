using MathSolver.Models;
using System.Globalization;
using System.Numerics;

namespace MathSolver.Services;

/// <summary>
/// Sinh 4 nhóm toán chuyển động cơ bản bằng Random: một vật, đuổi kịp,
/// gặp nhau và xuôi/ngược dòng. Không chứa các dạng nâng cao.
/// </summary>
public sealed class MotionQuizGenerator
{
    private enum MotionUnitKind
    {
        RoadKmHour,
        RoadKmMinute,
        MeterSecond,
        CentimeterSecond,
        MillimeterSecond,
        MilesHour
    }

    private enum MotionSubjectKind
    {
        MotorVehicle,
        Train,
        Bicycle,
        FastAnimal,
        MediumAnimal,
        TinyAnimal,
        Pedestrian,
        Runner
    }

    private sealed record MotionUnitProfile(MotionUnitKind Kind, string SpeedUnit, string TimeUnit, string DistanceUnit, int DistanceScale, int TimeDivisor, bool EnglishOnly = false);

    private sealed record MovingSubject(string Name, MotionSubjectKind Kind);

    // Đơn vị được chia thành các profile thực tế. Không random độc lập đơn vị
    // với đối tượng: ô tô/xe buýt không bao giờ nhận cm/mm; rùa/rùa cạn mới
    // có thể dùng cm hoặc mm với tốc độ tương ứng cm/s, mm/s.
    private static IReadOnlyList<MotionUnitProfile> UnitProfiles(AppLanguage language) => QuizContentCatalog.LoadList<MotionUnitProfile>("MotionQuizGenerator.UnitProfiles", QuizContentCatalog.Culture(language));

    private IReadOnlyList<MovingSubject> MovingSubjects(AppLanguage language) => NarrativeContextExpansion.Load<MovingSubject>("MotionQuizGenerator.MovingSubjects", language, _expandNarratives);

    private readonly Random _random;
    private readonly bool _expandNarratives;

    public MotionQuizGenerator(Random? random = null, bool expandNarratives = true)
    {
        _random = random ?? Random.Shared;
        _expandNarratives = expandNarratives;
    }

    public ArithmeticQuizQuestion GenerateAlgorithm(
        ArithmeticQuizMode mode,
        AppLanguage language,
        MotionQuizType? requestedType = null,
        QuizCurriculumContext? curriculumContext = null) =>
        CreateQuestion(
            mode,
            CreateContract(
                language,
                requestedType,
                curriculumContext));

    public ArithmeticQuizQuestion GenerateContract(
        ArithmeticQuizMode mode,
        AppLanguage language,
        MotionQuizType? requestedType = null,
        QuizCurriculumContext? curriculumContext = null) =>
        CreateQuestion(
            mode,
            CreateContract(
                language,
                requestedType,
                curriculumContext));

    private MotionQuizContract CreateContract(
        AppLanguage language,
        MotionQuizType? requestedType,
        QuizCurriculumContext? curriculumContext)
    {
        IReadOnlyList<MotionQuizType> allowedTypes =
            curriculumContext.HasValue
                ? QuizCurriculumLayer.GetAllowedMotionTypes(
                    curriculumContext.Value)
                : Enum.GetValues<MotionQuizType>();

        if (allowedTypes.Count == 0)
        {
            throw new InvalidOperationException(
                "Motion problems are not available at the selected curriculum tier.");
        }

        int? level = QuizDifficultyPolicy.Level(curriculumContext);
        allowedTypes = QuizDifficultyPolicy.Prefer(allowedTypes, requestedType, level,
            QuizDifficultyPolicy.MotionTypes);
        MotionQuizType type =
            requestedType.HasValue &&
            allowedTypes.Contains(requestedType.Value)
                ? requestedType.Value
                : allowedTypes[_random.Next(allowedTypes.Count)];

        return type switch
        {
            MotionQuizType.Basic => CreateBasicContract(language, level),
            MotionQuizType.Chasing => CreateChasingContract(language, level),
            MotionQuizType.Meeting => CreateMeetingContract(language, level),
            MotionQuizType.River => CreateRiverContract(language, level),
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };
    }

    private MotionQuizContract CreateBasicContract(AppLanguage language, int? level)
    {
        MotionQuestionKind kind = (level switch
        {
            1 => 0,
            2 => _random.Next(1, 3),
            3 => _random.Next(3),
            5 => 3,
            _ => _random.Next(4)
        }) switch
        {
            0 => MotionQuestionKind.BasicDistance,
            1 => MotionQuestionKind.BasicSpeed,
            2 => MotionQuestionKind.BasicTime,
            _ => MotionQuestionKind.BasicRestDistance
        };

        (MovingSubject movingSubject, MotionUnitProfile profile) =
            PickMovingSubjectAndProfile(language, kind == MotionQuestionKind.BasicRestDistance, level);
        string subject = GetSubjectText(movingSubject, language);
        string speedUnit = GetSpeedUnit(profile, language);
        string timeUnit = GetTimeUnit(profile, language);
        string distanceUnit = GetDistanceUnit(profile, language);

        if (kind == MotionQuestionKind.BasicRestDistance)
        {
            // At five stars, deduct rest time before converting minutes to hours.
            // Lower tiers keep matching speed/time units for this variant.
            (int speed, int travelTime, int distance) = CreateSpeedTimeDistance(profile, movingSubject.Kind, level);
            int restTime = _random.Next(1, travelTime + 1);
            int totalElapsed = travelTime + restTime;

            string problem = QuizContentCatalog.Text(language, "MotionQuizGenerator.CreateBasicContract.001", ("Capitalize_subject", $"{Capitalize(subject)}"), ("speed", $"{speed}"), ("speedUnit", $"{speedUnit}"), ("totalElapsed", $"{totalElapsed}"), ("timeUnit", $"{timeUnit}"), ("restTime", $"{restTime}"), ("subject", $"{subject}"), ("distanceUnit", $"{distanceUnit}"));

            string calculation = MultiplyIfNeeded(
                $"({totalElapsed} - {restTime}) × {speed}",
                profile.DistanceScale);
            calculation = DivideIfNeeded(calculation, profile.TimeDivisor);
            string equation = $"{calculation} = {distance}";
            string solution = QuizContentCatalog.Text(language, "MotionQuizGenerator.CreateBasicContract.015", ("Capitalize_subject", $"{Capitalize(subject)}"), ("Environment_NewLine", $"{Environment.NewLine}"), ("equation", $"{equation}"), ("distanceUnit", $"{distanceUnit}"), ("subject", $"{subject}"));

            return new(
                MotionQuizType.Basic,
                kind,
                [speed, totalElapsed, restTime],
                distance,
                distanceUnit,
                QuizContentCatalog.Text(language, "MotionQuizGenerator.CreateBasicContract.002"),
                problem,
                equation,
                solution,
                profile.TimeDivisor == 1 ? travelTime : speed * travelTime,
                profile.TimeDivisor == 1 ? ArithmeticOperation.Multiply : ArithmeticOperation.Divide,
                profile.TimeDivisor == 1 ? speed * profile.DistanceScale : profile.TimeDivisor,
                DistinctUnits(speedUnit, timeUnit, distanceUnit)) { NarrativeActors = [subject] };
        }

        if (kind == MotionQuestionKind.BasicDistance)
        {
            (int speed, int time, int distance) = CreateSpeedTimeDistance(profile, movingSubject.Kind, level);
            string problem = QuizContentCatalog.Text(language, "MotionQuizGenerator.CreateBasicContract.003", ("Capitalize_subject", $"{Capitalize(subject)}"), ("speed", $"{speed}"), ("speedUnit", $"{speedUnit}"), ("time", $"{time}"), ("timeUnit", $"{timeUnit}"), ("distanceUnit", $"{distanceUnit}"));
            string calculation = MultiplyIfNeeded(
                DivideIfNeeded($"{speed} × {time}", profile.TimeDivisor),
                profile.DistanceScale);
            string equation = $"{calculation} = {distance}";
            string solution = QuizContentCatalog.Text(language, "MotionQuizGenerator.CreateBasicContract.016", ("Capitalize_subject", $"{Capitalize(subject)}"), ("Environment_NewLine", $"{Environment.NewLine}"), ("equation", $"{equation}"), ("distanceUnit", $"{distanceUnit}"), ("subject", $"{subject}"));

            return new(
                MotionQuizType.Basic,
                kind,
                [speed, time],
                distance,
                distanceUnit,
                QuizContentCatalog.Text(language, "MotionQuizGenerator.CreateBasicContract.004"),
                problem,
                equation,
                solution,
                profile.TimeDivisor == 1 ? speed : speed * time,
                profile.TimeDivisor == 1 ? ArithmeticOperation.Multiply : ArithmeticOperation.Divide,
                profile.TimeDivisor == 1 ? time * profile.DistanceScale : profile.TimeDivisor,
                DistinctUnits(speedUnit, timeUnit, distanceUnit)) { NarrativeActors = [subject] };
        }

        if (kind == MotionQuestionKind.BasicSpeed)
        {
            (int speed, int time, int distance) = CreateSpeedTimeDistance(profile, movingSubject.Kind, level);
            string problem = QuizContentCatalog.Text(language, "MotionQuizGenerator.CreateBasicContract.017", ("Capitalize_subject", $"{Capitalize(subject)}"), ("distance", $"{distance}"), ("distanceUnit", $"{distanceUnit}"), ("time", $"{time}"), ("timeUnit", $"{timeUnit}"), ("subject", $"{subject}"), ("speedUnit", $"{speedUnit}"));
            int normalizedDistance = distance / profile.DistanceScale;
            int numerator = checked(normalizedDistance * profile.TimeDivisor);
            string calculation = MultiplyIfNeeded(
                $"{DivideIfNeeded(distance.ToString(CultureInfo.InvariantCulture), profile.DistanceScale)} ÷ {time}",
                profile.TimeDivisor);
            string equation = $"{calculation} = {speed}";
            string solution = QuizContentCatalog.Text(language, "MotionQuizGenerator.CreateBasicContract.005", ("subject", $"{subject}"), ("Environment_NewLine", $"{Environment.NewLine}"), ("equation", $"{equation}"), ("speedUnit", $"{speedUnit}"));

            return new(
                MotionQuizType.Basic,
                kind,
                [distance, time],
                speed,
                speedUnit,
                QuizContentCatalog.Text(language, "MotionQuizGenerator.CreateBasicContract.006"),
                problem,
                equation,
                solution,
                numerator,
                ArithmeticOperation.Divide,
                time,
                DistinctUnits(distanceUnit, timeUnit, speedUnit)) { NarrativeActors = [subject] };
        }

        // BasicTime
        (int targetSpeed, int targetTime, int targetDistance) =
            CreateSpeedTimeDistance(profile, movingSubject.Kind, level);
        string timeProblem = QuizContentCatalog.Text(language, "MotionQuizGenerator.CreateBasicContract.018", ("Capitalize_subject", $"{Capitalize(subject)}"), ("targetSpeed", $"{targetSpeed}"), ("speedUnit", $"{speedUnit}"), ("targetDistance", $"{targetDistance}"), ("distanceUnit", $"{distanceUnit}"), ("subject", $"{subject}"), ("timeUnit", $"{timeUnit}"));
        int normalizedTargetDistance = targetDistance / profile.DistanceScale;
        int timeNumerator = checked(normalizedTargetDistance * profile.TimeDivisor);
        string timeCalculation = MultiplyIfNeeded(
            $"{DivideIfNeeded(targetDistance.ToString(CultureInfo.InvariantCulture), profile.DistanceScale)} ÷ {targetSpeed}",
            profile.TimeDivisor);
        string timeEquation = $"{timeCalculation} = {targetTime}";
        string timeSolution = QuizContentCatalog.Text(language, "MotionQuizGenerator.CreateBasicContract.007", ("subject", $"{subject}"), ("Environment_NewLine", $"{Environment.NewLine}"), ("timeEquation", $"{timeEquation}"), ("timeUnit", $"{timeUnit}"));

        return new(
            MotionQuizType.Basic,
            kind,
            [targetSpeed, targetDistance],
            targetTime,
            timeUnit,
            QuizContentCatalog.Text(language, "MotionQuizGenerator.CreateBasicContract.008"),
            timeProblem,
            timeEquation,
            timeSolution,
            timeNumerator,
            ArithmeticOperation.Divide,
            targetSpeed,
            DistinctUnits(speedUnit, distanceUnit, timeUnit)) { NarrativeActors = [subject] };
    }

    private MotionQuizContract CreateChasingContract(AppLanguage language, int? level)
    {
        (MovingSubject slowMovingSubject, MovingSubject fastMovingSubject, MotionUnitProfile profile) =
            PickMovingSubjectPairAndProfile(language, level);
        string slowSubject = GetSubjectText(slowMovingSubject, language);
        string fastSubject = GetSubjectText(fastMovingSubject, language);

        string speedUnit = GetSpeedUnit(profile, language);
        string timeUnit = GetTimeUnit(profile, language);
        string distanceUnit = GetDistanceUnit(profile, language);

        (int slowSpeed, int fastSpeed, int time, int gap) =
            CreateChasingNumbers(profile, slowMovingSubject.Kind, level);

        string problem = QuizContentCatalog.Text(language, "MotionQuizGenerator.CreateChasingContract.009", ("Capitalize_slowSubject", $"{Capitalize(slowSubject)}"), ("fastSubject", $"{fastSubject}"), ("gap", $"{gap}"), ("distanceUnit", $"{distanceUnit}"), ("slowSpeed", $"{slowSpeed}"), ("speedUnit", $"{speedUnit}"), ("fastSpeed", $"{fastSpeed}"), ("timeUnit", $"{timeUnit}"));

        int relativeSpeed = fastSpeed - slowSpeed;
        string calculation = MultiplyIfNeeded(
            $"{DivideIfNeeded(gap.ToString(CultureInfo.InvariantCulture), profile.DistanceScale)} ÷ ({fastSpeed} - {slowSpeed})",
            profile.TimeDivisor);
        string equation = $"{calculation} = {time}";
        string solution = QuizContentCatalog.Text(language, "MotionQuizGenerator.CreateChasingContract.010", ("fastSubject", $"{fastSubject}"), ("slowSubject", $"{slowSubject}"), ("Environment_NewLine", $"{Environment.NewLine}"), ("equation", $"{equation}"), ("timeUnit", $"{timeUnit}"));

        BigInteger numerator = profile.TimeDivisor == 1
            ? gap / profile.DistanceScale
            : (BigInteger)gap * profile.TimeDivisor;

        return new(
            MotionQuizType.Chasing,
            MotionQuestionKind.CatchUpTime,
            [gap, slowSpeed, fastSpeed],
            time,
            timeUnit,
            QuizContentCatalog.Text(language, "MotionQuizGenerator.CreateChasingContract.011"),
            problem,
            equation,
            solution,
            numerator,
            ArithmeticOperation.Divide,
            relativeSpeed,
            DistinctUnits(distanceUnit, speedUnit, timeUnit)) { NarrativeActors = [slowSubject, fastSubject] };
    }

    private MotionQuizContract CreateMeetingContract(AppLanguage language, int? level)
    {
        (MovingSubject movingSubject1, MovingSubject movingSubject2, MotionUnitProfile profile) =
            PickMovingSubjectPairAndProfile(language, level);
        string subject1 = GetSubjectText(movingSubject1, language);
        string subject2 = GetSubjectText(movingSubject2, language);

        string speedUnit = GetSpeedUnit(profile, language);
        string timeUnit = GetTimeUnit(profile, language);
        string distanceUnit = GetDistanceUnit(profile, language);

        (int speed1, int speed2, int time, int distance) =
            CreateMeetingNumbers(profile, movingSubject1.Kind, level);

        string problem = QuizContentCatalog.Text(language, "MotionQuizGenerator.CreateMeetingContract.012", ("Capitalize_subject1", $"{Capitalize(subject1)}"), ("subject2", $"{subject2}"), ("distance", $"{distance}"), ("distanceUnit", $"{distanceUnit}"), ("speed1", $"{speed1}"), ("speedUnit", $"{speedUnit}"), ("speed2", $"{speed2}"), ("timeUnit", $"{timeUnit}"));

        int relativeSpeed = speed1 + speed2;
        string calculation = MultiplyIfNeeded(
            $"{DivideIfNeeded(distance.ToString(CultureInfo.InvariantCulture), profile.DistanceScale)} ÷ ({speed1} + {speed2})",
            profile.TimeDivisor);
        string equation = $"{calculation} = {time}";
        string solution = QuizContentCatalog.Text(language, "MotionQuizGenerator.CreateMeetingContract.013", ("subject1", $"{subject1}"), ("subject2", $"{subject2}"), ("Environment_NewLine", $"{Environment.NewLine}"), ("equation", $"{equation}"), ("timeUnit", $"{timeUnit}"));

        BigInteger numerator = profile.TimeDivisor == 1
            ? distance / profile.DistanceScale
            : (BigInteger)distance * profile.TimeDivisor;

        return new(
            MotionQuizType.Meeting,
            MotionQuestionKind.MeetingTime,
            [distance, speed1, speed2],
            time,
            timeUnit,
            QuizContentCatalog.Text(language, "MotionQuizGenerator.CreateMeetingContract.014"),
            problem,
            equation,
            solution,
            numerator,
            ArithmeticOperation.Divide,
            relativeSpeed,
            DistinctUnits(distanceUnit, speedUnit, timeUnit)) { NarrativeActors = [subject1, subject2] };
    }

    private MotionQuizContract CreateRiverContract(AppLanguage language, int? level)
    {
        MotionQuestionKind kind = (level switch
        {
            <= 2 => _random.Next(2),
            3 => _random.Next(3),
            >= 4 => _random.Next(2, 4),
            _ => _random.Next(4)
        }) switch
        {
            0 => MotionQuestionKind.RiverDownstreamSpeed,
            1 => MotionQuestionKind.RiverUpstreamSpeed,
            2 => MotionQuestionKind.RiverBoatSpeed,
            _ => MotionQuestionKind.RiverCurrentSpeed
        };

        MotionUnitProfile profile = PickRiverProfile(language);
        string craft = PickWatercraft(language);
        string speedUnit = GetSpeedUnit(profile, language);
        int boatSpeed = profile.SpeedUnit == "m/s"
            ? _random.Next(5, 16)
            : profile.SpeedUnit == "mph"
                ? _random.Next(15, 41)
                : _random.Next(18, 46);
        int currentSpeed = profile.SpeedUnit == "m/s"
            ? _random.Next(1, Math.Min(3, boatSpeed))
            : _random.Next(1, Math.Min(6, boatSpeed));
        int downstream = boatSpeed + currentSpeed;
        int upstream = boatSpeed - currentSpeed;

        return kind switch
        {
            MotionQuestionKind.RiverDownstreamSpeed =>
                CreateRiverSimpleContract(
                    language,
                    kind,
                    craft,
                    boatSpeed,
                    currentSpeed,
                    downstream,
                    speedUnit,
                    isDownstream: true),
            MotionQuestionKind.RiverUpstreamSpeed =>
                CreateRiverSimpleContract(
                    language,
                    kind,
                    craft,
                    boatSpeed,
                    currentSpeed,
                    upstream,
                    speedUnit,
                    isDownstream: false),
            MotionQuestionKind.RiverBoatSpeed =>
                CreateRiverDerivedContract(
                    language,
                    kind,
                    craft,
                    downstream,
                    upstream,
                    boatSpeed,
                    speedUnit,
                    findBoat: true),
            _ =>
                CreateRiverDerivedContract(
                    language,
                    kind,
                    craft,
                    downstream,
                    upstream,
                    currentSpeed,
                    speedUnit,
                    findBoat: false)
        };
    }

    private static MotionQuizContract CreateRiverSimpleContract(
        AppLanguage language,
        MotionQuestionKind kind,
        string craft,
        int boatSpeed,
        int currentSpeed,
        int answer,
        string speedUnit,
        bool isDownstream)
    {
        string problem = QuizContentCatalog.Text(language, "MotionQuizGenerator.CreateRiverSimpleContract.019", ("Capitalize_craft", $"{Capitalize(craft)}"), ("boatSpeed", $"{boatSpeed}"), ("speedUnit", $"{speedUnit}"), ("currentSpeed", $"{currentSpeed}"), ("isDownstream_xu_i_d_ng_ng_c_d_ng", $"{(isDownstream ? "xuôi dòng" : "ngược dòng")}"), ("craft", $"{craft}"), ("isDownstream_downstream_upstream", $"{(isDownstream ? "downstream" : "upstream")}"));
        string equation = isDownstream
            ? $"{boatSpeed} + {currentSpeed} = {answer}"
            : $"{boatSpeed} - {currentSpeed} = {answer}";
        string solution = QuizContentCatalog.Text(language, "MotionQuizGenerator.CreateRiverSimpleContract.020", ("isDownstream_xu_i_d_ng_ng_c_d_ng", $"{(isDownstream ? "xuôi dòng" : "ngược dòng")}"), ("craft", $"{craft}"), ("Environment_NewLine", $"{Environment.NewLine}"), ("equation", $"{equation}"), ("speedUnit", $"{speedUnit}"), ("isDownstream_downstream_upstream", $"{(isDownstream ? "downstream" : "upstream")}"));

        return new(
            MotionQuizType.River,
            kind,
            [boatSpeed, currentSpeed],
            answer,
            speedUnit,
            (isDownstream ? QuizContentCatalog.Text(language, "MotionQuizGenerator.CreateRiverSimpleContract.022") : QuizContentCatalog.Text(language, "MotionQuizGenerator.CreateRiverSimpleContract.023")),
            problem,
            equation,
            solution,
            boatSpeed,
            isDownstream ? ArithmeticOperation.Add : ArithmeticOperation.Subtract,
            currentSpeed,
            DistinctUnits(speedUnit)) { NarrativeActors = [craft] };
    }

    private static MotionQuizContract CreateRiverDerivedContract(
        AppLanguage language,
        MotionQuestionKind kind,
        string craft,
        int downstream,
        int upstream,
        int answer,
        string speedUnit,
        bool findBoat)
    {
        string problem = QuizContentCatalog.Text(language, "MotionQuizGenerator.CreateRiverDerivedContract.021", ("Capitalize_craft", $"{Capitalize(craft)}"), ("downstream", $"{downstream}"), ("speedUnit", $"{speedUnit}"), ("upstream", $"{upstream}"), ("value4", $"{(findBoat ? "vận tốc của thuyền khi nước yên" : "vận tốc dòng nước")}"), ("findBoat_speed_in_still_water_current_speed", $"{(findBoat ? "speed in still water" : "current speed")}"));
        int numerator = findBoat
            ? downstream + upstream
            : downstream - upstream;
        string equation = findBoat
            ? $"({downstream} + {upstream}) ÷ 2 = {answer}"
            : $"({downstream} - {upstream}) ÷ 2 = {answer}";
        string solution = (findBoat ? QuizContentCatalog.Text(language, "MotionQuizGenerator.CreateRiverDerivedContract.024", ("Environment_NewLine", $"{Environment.NewLine}"), ("equation", $"{equation}"), ("speedUnit", $"{speedUnit}"), ("craft", $"{craft}")) : QuizContentCatalog.Text(language, "MotionQuizGenerator.CreateRiverDerivedContract.025", ("Environment_NewLine", $"{Environment.NewLine}"), ("equation", $"{equation}"), ("speedUnit", $"{speedUnit}")));

        return new(
            MotionQuizType.River,
            kind,
            [downstream, upstream],
            answer,
            speedUnit,
            (findBoat ? QuizContentCatalog.Text(language, "MotionQuizGenerator.CreateRiverDerivedContract.026") : QuizContentCatalog.Text(language, "MotionQuizGenerator.CreateRiverDerivedContract.027")),
            problem,
            equation,
            solution,
            numerator,
            ArithmeticOperation.Divide,
            2,
            DistinctUnits(speedUnit)) { NarrativeActors = [craft] };
    }

    private ArithmeticQuizQuestion CreateQuestion(
        ArithmeticQuizMode mode,
        MotionQuizContract contract)
    {
        var expression = new IntegerArithmeticExpression(
            contract.RepresentativeLeft,
            contract.RepresentativeOperation,
            contract.RepresentativeRight);

        return mode switch
        {
            ArithmeticQuizMode.TrueFalse =>
                CreateTrueFalseQuestion(expression, contract),
            ArithmeticQuizMode.MultipleChoice =>
                CreateMultipleChoiceQuestion(expression, contract),
            ArithmeticQuizMode.Essay =>
                new(
                    expression,
                    mode,
                    contract.CorrectAnswer,
                    null,
                    null,
                    [],
                    MotionProblem: contract),
            _ => throw new ArgumentOutOfRangeException(nameof(mode))
        };
    }

    private ArithmeticQuizQuestion CreateTrueFalseQuestion(
        IntegerArithmeticExpression expression,
        MotionQuizContract contract)
    {
        bool showCorrect = _random.Next(2) == 0;
        BigInteger shown = showCorrect
            ? contract.CorrectAnswer
            : CreateDistractors(contract.CorrectAnswer, 1)[0];

        return new(
            expression,
            ArithmeticQuizMode.TrueFalse,
            contract.CorrectAnswer,
            shown,
            shown == contract.CorrectAnswer,
            [],
            MotionProblem: contract);
    }

    private ArithmeticQuizQuestion CreateMultipleChoiceQuestion(
        IntegerArithmeticExpression expression,
        MotionQuizContract contract)
    {
        var choices = new List<BigInteger> { contract.CorrectAnswer };
        choices.AddRange(CreateDistractors(contract.CorrectAnswer, 3));
        Shuffle(choices);

        return new(
            expression,
            ArithmeticQuizMode.MultipleChoice,
            contract.CorrectAnswer,
            null,
            null,
            choices,
            MotionProblem: contract);
    }

    private IReadOnlyList<BigInteger> CreateDistractors(
        BigInteger correct,
        int count)
    {
        var set = new HashSet<BigInteger>();
        BigInteger magnitude = BigInteger.Abs(correct);
        BigInteger step = magnitude >= 1000
            ? 100
            : magnitude >= 100
                ? 10
                : magnitude >= 20
                    ? 2
                    : 1;
        int[] offsets = [-5, -3, -2, -1, 1, 2, 3, 5];
        int start = _random.Next(offsets.Length);

        for (int i = 0; i < offsets.Length && set.Count < count; i++)
        {
            BigInteger candidate = correct + step * offsets[(start + i) % offsets.Length];
            if (candidate > 0 && candidate != correct)
            {
                set.Add(candidate);
            }
        }

        while (set.Count < count)
        {
            BigInteger candidate = correct + step * (set.Count + 1);
            if (candidate > 0 && candidate != correct)
            {
                set.Add(candidate);
            }
        }

        return set.ToArray();
    }

    private (int Speed, int Time, int Distance)
        CreateSpeedTimeDistance(
            MotionUnitProfile profile,
            MotionSubjectKind subjectKind, int? level)
    {
        for (int attempt = 0; attempt < 128; attempt++)
        {
            int speed = PickRealisticSpeed(subjectKind, profile);
            int time = profile.TimeDivisor switch
            {
                60 => PickMinuteDuration(level),
                _ when profile.Kind is MotionUnitKind.MeterSecond or MotionUnitKind.CentimeterSecond or MotionUnitKind.MillimeterSecond => _random.Next(5, level.HasValue ? 6 + 10 * level.Value : 61),
                _ => _random.Next(2, level.HasValue ? level.Value + 3 : 6)
            };

            int numerator = checked(speed * time);
            if (numerator % profile.TimeDivisor != 0)
            {
                continue;
            }

            int baseDistance = numerator / profile.TimeDivisor;
            int distance = checked(baseDistance * profile.DistanceScale);
            if (distance > 0)
            {
                return (speed, time, distance);
            }
        }

        int fallbackSpeed = PickRealisticSpeed(subjectKind, profile);
        int fallbackTime = profile.TimeDivisor == 60 ? 60 : 2;
        int fallbackDistance = checked(
            fallbackSpeed * fallbackTime /
            profile.TimeDivisor * profile.DistanceScale);
        return (fallbackSpeed, fallbackTime, Math.Max(1, fallbackDistance));
    }

    private (int Slow, int Fast, int Time, int Gap)
        CreateChasingNumbers(
            MotionUnitProfile profile,
            MotionSubjectKind subjectKind, int? level)
    {
        for (int attempt = 0; attempt < 128; attempt++)
        {
            int time = PickTimeForRelativeProfile(profile, level);
            int first = PickRealisticSpeed(subjectKind, profile);
            int second = PickRealisticSpeed(subjectKind, profile);
            if (first == second)
            {
                continue;
            }

            int slow = Math.Min(first, second);
            int fast = Math.Max(first, second);
            int difference = fast - slow;
            int product = checked(difference * time);
            if (product % profile.TimeDivisor != 0)
            {
                continue;
            }

            int gap = checked(
                product / profile.TimeDivisor * profile.DistanceScale);
            if (gap > 0)
            {
                return (slow, fast, time, gap);
            }
        }

        (int min, int max) = GetSpeedRange(subjectKind, profile.Kind);
        int fallbackSlow = min;
        int fallbackFast = Math.Max(min + 1, Math.Min(max - 1, min + Math.Max(1, (max - min) / 2)));
        int fallbackTime = profile.TimeDivisor == 60 ? 60 : 2;
        int fallbackGap = checked(
            (fallbackFast - fallbackSlow) * fallbackTime /
            profile.TimeDivisor * profile.DistanceScale);
        return (fallbackSlow, fallbackFast, fallbackTime, Math.Max(1, fallbackGap));
    }

    private (int Speed1, int Speed2, int Time, int Distance)
        CreateMeetingNumbers(
            MotionUnitProfile profile,
            MotionSubjectKind subjectKind, int? level)
    {
        for (int attempt = 0; attempt < 128; attempt++)
        {
            int time = PickTimeForRelativeProfile(profile, level);
            int speed1 = PickRealisticSpeed(subjectKind, profile);
            int speed2 = PickRealisticSpeed(subjectKind, profile);

            int relative = speed1 + speed2;
            int product = checked(relative * time);
            if (product % profile.TimeDivisor != 0)
            {
                continue;
            }

            int distance = checked(
                product / profile.TimeDivisor * profile.DistanceScale);

            if (distance > 0)
            {
                return (speed1, speed2, time, distance);
            }
        }

        int fallbackTime = profile.TimeDivisor == 60 ? 60 : 2;
        int fallbackSpeed1 = PickRealisticSpeed(subjectKind, profile);
        int fallbackSpeed2 = PickRealisticSpeed(subjectKind, profile);
        int fallbackDistance = checked(
            (fallbackSpeed1 + fallbackSpeed2) * fallbackTime /
            profile.TimeDivisor * profile.DistanceScale);
        return (fallbackSpeed1, fallbackSpeed2, fallbackTime, Math.Max(1, fallbackDistance));
    }

    private int PickMinuteDuration(int? level) => level switch
    {
        4 => Pick(new[] { 15, 20, 30, 45, 60 }),
        5 => Pick(new[] { 12, 18, 24, 36, 42, 48, 54, 72, 84, 96 }),
        _ => Pick(new[] { 10, 15, 20, 30, 45, 60, 90, 120 })
    };

    private int PickTimeForRelativeProfile(MotionUnitProfile profile, int? level) =>
        profile.TimeDivisor == 60
            ? PickMinuteDuration(level)
            : profile.Kind is MotionUnitKind.MeterSecond or MotionUnitKind.CentimeterSecond or MotionUnitKind.MillimeterSecond
                ? _random.Next(5, level.HasValue ? 6 + 10 * level.Value : 61)
                : _random.Next(1, level.HasValue ? level.Value + 2 : 5);

    private (MovingSubject Subject, MotionUnitProfile Profile)
        PickMovingSubjectAndProfile(
            AppLanguage language,
            bool restOnly, int? level)
    {
        MovingSubject[] subjects = level is >= 4
            ? MovingSubjects(language).Where(subject => subject.Kind is MotionSubjectKind.MotorVehicle or MotionSubjectKind.Train).ToArray()
            : MovingSubjects(language).ToArray();
        MovingSubject subject = Pick(subjects);
        MotionUnitProfile profile = PickProfileForSubject(
            subject.Kind,
            language,
            restOnly, level);
        return (subject, profile);
    }

    private (MovingSubject First, MovingSubject Second, MotionUnitProfile Profile)
        PickMovingSubjectPairAndProfile(AppLanguage language, int? level)
    {
        MotionSubjectKind[] pairKinds = MovingSubjects(language)
            .GroupBy(subject => subject.Kind)
            // Train contexts model a single journey. Adding a second named train
            // must not implicitly enable chase/meeting stories without a rail model.
            .Where(group => group.Key != MotionSubjectKind.Train && group.Count() >= 2
                && (level is not >= 4 || group.Key == MotionSubjectKind.MotorVehicle))
            .Select(group => group.Key)
            .ToArray();

        MotionSubjectKind kind = Pick(pairKinds);
        MovingSubject[] candidates = MovingSubjects(language)
            .Where(subject => subject.Kind == kind)
            .ToArray();

        MovingSubject first = Pick(candidates);
        MovingSubject second;
        do
        {
            second = Pick(candidates);
        }
        while (ReferenceEquals(first, second));

        MotionUnitProfile profile = PickProfileForSubject(
            kind,
            language,
            restOnly: false, level);

        return (first, second, profile);
    }

    private MotionUnitProfile PickProfileForSubject(
        MotionSubjectKind kind,
        AppLanguage language,
        bool restOnly, int? level)
    {
        MotionUnitKind[] allowedKinds = kind switch
        {
            MotionSubjectKind.MotorVehicle =>
                language == AppLanguage.English
                    ? [MotionUnitKind.RoadKmHour, MotionUnitKind.RoadKmMinute, MotionUnitKind.MeterSecond, MotionUnitKind.MilesHour]
                    : [MotionUnitKind.RoadKmHour, MotionUnitKind.RoadKmMinute, MotionUnitKind.MeterSecond],
            MotionSubjectKind.Train =>
                language == AppLanguage.English
                    ? [MotionUnitKind.RoadKmHour, MotionUnitKind.RoadKmMinute, MotionUnitKind.MilesHour]
                    : [MotionUnitKind.RoadKmHour, MotionUnitKind.RoadKmMinute],
            MotionSubjectKind.Bicycle =>
                language == AppLanguage.English
                    ? [MotionUnitKind.RoadKmHour, MotionUnitKind.MeterSecond, MotionUnitKind.MilesHour]
                    : [MotionUnitKind.RoadKmHour, MotionUnitKind.MeterSecond],
            MotionSubjectKind.FastAnimal =>
                language == AppLanguage.English
                    ? [MotionUnitKind.RoadKmHour, MotionUnitKind.MeterSecond, MotionUnitKind.MilesHour]
                    : [MotionUnitKind.RoadKmHour, MotionUnitKind.MeterSecond],
            MotionSubjectKind.MediumAnimal =>
                [MotionUnitKind.MeterSecond],
            MotionSubjectKind.TinyAnimal =>
                [MotionUnitKind.CentimeterSecond, MotionUnitKind.MillimeterSecond],
            MotionSubjectKind.Pedestrian =>
                [MotionUnitKind.MeterSecond],
            MotionSubjectKind.Runner =>
                [MotionUnitKind.MeterSecond],
            _ => [MotionUnitKind.MeterSecond]
        };

        MotionUnitProfile[] candidates = UnitProfiles(language)
            .Where(profile =>
                allowedKinds.Contains(profile.Kind) &&
                (!profile.EnglishOnly || language == AppLanguage.English) &&
                (!restOnly || level == 5 || profile.TimeDivisor == 1) &&
                (level is not <= 2 || profile.TimeDivisor == 1) &&
                (level is not >= 4 || (restOnly && level == 4) || profile.TimeDivisor == 60))
            .ToArray();

        return Pick(candidates);
    }

    private MotionUnitProfile PickRiverProfile(AppLanguage language)
    {
        MotionUnitKind[] allowedKinds = language == AppLanguage.English
            ? [MotionUnitKind.RoadKmHour, MotionUnitKind.MeterSecond, MotionUnitKind.MilesHour]
            : [MotionUnitKind.RoadKmHour, MotionUnitKind.MeterSecond];

        MotionUnitProfile[] candidates = UnitProfiles(language)
            .Where(profile =>
                allowedKinds.Contains(profile.Kind) &&
                (!profile.EnglishOnly || language == AppLanguage.English) &&
                profile.TimeDivisor == 1)
            .ToArray();
        return Pick(candidates);
    }

    private int PickRealisticSpeed(
        MotionSubjectKind subjectKind,
        MotionUnitProfile profile)
    {
        (int min, int maxExclusive) = GetSpeedRange(subjectKind, profile.Kind);
        return _random.Next(min, maxExclusive);
    }

    private static (int Min, int MaxExclusive) GetSpeedRange(
        MotionSubjectKind subjectKind,
        MotionUnitKind unitKind) =>
        (subjectKind, unitKind) switch
        {
            (MotionSubjectKind.MotorVehicle, MotionUnitKind.RoadKmHour or MotionUnitKind.RoadKmMinute) => (25, 91),
            (MotionSubjectKind.MotorVehicle, MotionUnitKind.MeterSecond) => (8, 24),
            (MotionSubjectKind.MotorVehicle, MotionUnitKind.MilesHour) => (15, 61),
            (MotionSubjectKind.Train, MotionUnitKind.RoadKmHour or MotionUnitKind.RoadKmMinute) => (60, 121),
            (MotionSubjectKind.Train, MotionUnitKind.MilesHour) => (40, 76),
            (MotionSubjectKind.Bicycle, MotionUnitKind.RoadKmHour) => (8, 31),
            (MotionSubjectKind.Bicycle, MotionUnitKind.MeterSecond) => (2, 9),
            (MotionSubjectKind.Bicycle, MotionUnitKind.MilesHour) => (5, 20),
            (MotionSubjectKind.FastAnimal, MotionUnitKind.RoadKmHour) => (15, 61),
            (MotionSubjectKind.FastAnimal, MotionUnitKind.MeterSecond) => (4, 16),
            (MotionSubjectKind.FastAnimal, MotionUnitKind.MilesHour) => (10, 36),
            (MotionSubjectKind.MediumAnimal, MotionUnitKind.MeterSecond) => (2, 11),
            (MotionSubjectKind.TinyAnimal, MotionUnitKind.CentimeterSecond) => (1, 9),
            (MotionSubjectKind.TinyAnimal, MotionUnitKind.MillimeterSecond) => (2, 21),
            (MotionSubjectKind.Pedestrian, MotionUnitKind.MeterSecond) => (1, 3),
            (MotionSubjectKind.Runner, MotionUnitKind.MeterSecond) => (2, 9),
            _ => (2, 12)
        };

    private static string GetSubjectText(
        MovingSubject subject,
        AppLanguage language) =>
        subject.Name;

    private string PickWatercraft(AppLanguage language) => Pick(QuizContentCatalog.LoadList<string>("MotionQuizGenerator.Watercraft", QuizContentCatalog.Culture(language)));

    private T Pick<T>(IReadOnlyList<T> values) =>
        values[_random.Next(values.Count)];

    private static string MultiplyIfNeeded(string expression, int factor) =>
        factor == 1 ? expression : $"{expression} × {factor}";

    private static string DivideIfNeeded(string expression, int divisor) =>
        divisor == 1 ? expression : $"{expression} ÷ {divisor}";

    private static IReadOnlyList<string> DistinctUnits(params string[] units) =>
        units
            .Where(unit => !string.IsNullOrWhiteSpace(unit))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static string GetSpeedUnit(MotionUnitProfile profile, AppLanguage language) =>
        profile.SpeedUnit;

    private static string GetTimeUnit(MotionUnitProfile profile, AppLanguage language) =>
        profile.TimeUnit;

    private static string GetDistanceUnit(MotionUnitProfile profile, AppLanguage language) =>
        profile.DistanceUnit;

    private static string Capitalize(string value) =>
        string.IsNullOrEmpty(value)
            ? value
            : char.ToUpper(value[0], CultureInfo.CurrentCulture) + value[1..];

    private void Shuffle<T>(IList<T> values)
    {
        for (int i = values.Count - 1; i > 0; i--)
        {
            int j = _random.Next(i + 1);
            (values[i], values[j]) = (values[j], values[i]);
        }
    }
}
