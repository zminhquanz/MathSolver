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
        Bicycle,
        FastAnimal,
        MediumAnimal,
        TinyAnimal,
        Pedestrian,
        Runner
    }

    private sealed record MotionUnitProfile(
        MotionUnitKind Kind,
        string SpeedUnitVi,
        string SpeedUnitEn,
        string TimeUnitVi,
        string TimeUnitEn,
        string DistanceUnitVi,
        string DistanceUnitEn,
        int DistanceScale,
        int TimeDivisor,
        bool EnglishOnly = false);

    private sealed record MovingSubject(
        string Vietnamese,
        string English,
        MotionSubjectKind Kind);

    // Đơn vị được chia thành các profile thực tế. Không random độc lập đơn vị
    // với đối tượng: ô tô/xe buýt không bao giờ nhận cm/mm; rùa/rùa cạn mới
    // có thể dùng cm hoặc mm với tốc độ tương ứng cm/s, mm/s.
    private static readonly MotionUnitProfile[] UnitProfiles =
    [
        new(MotionUnitKind.RoadKmHour, "km/h", "km/h", "giờ", "hours", "km", "km", 1, 1),
        new(MotionUnitKind.RoadKmMinute, "km/h", "km/h", "phút", "minutes", "km", "km", 1, 60),
        new(MotionUnitKind.MeterSecond, "m/s", "m/s", "giây", "seconds", "m", "m", 1, 1),
        new(MotionUnitKind.CentimeterSecond, "cm/s", "cm/s", "giây", "seconds", "cm", "cm", 1, 1),
        new(MotionUnitKind.MillimeterSecond, "mm/s", "mm/s", "giây", "seconds", "mm", "mm", 1, 1),
        new(MotionUnitKind.MilesHour, "mph", "mph", "giờ", "hours", "dặm", "miles", 1, 1, EnglishOnly: true)
    ];

    private static readonly MovingSubject[] MovingSubjects =
    [
        new("một ô tô", "a car", MotionSubjectKind.MotorVehicle),
        new("một xe máy", "a motorcycle", MotionSubjectKind.MotorVehicle),
        new("một xe buýt", "a bus", MotionSubjectKind.MotorVehicle),
        new("một tàu hỏa", "a train", MotionSubjectKind.MotorVehicle),
        new("một xe đạp", "a bicycle", MotionSubjectKind.Bicycle),
        new("một con ngựa", "a horse", MotionSubjectKind.FastAnimal),
        new("một con nai", "a deer", MotionSubjectKind.FastAnimal),
        new("một con chó", "a dog", MotionSubjectKind.MediumAnimal),
        new("một con thỏ", "a rabbit", MotionSubjectKind.MediumAnimal),
        new("một con rùa", "a turtle", MotionSubjectKind.TinyAnimal),
        new("một con rùa cạn", "a tortoise", MotionSubjectKind.TinyAnimal),
        new("một người đi bộ", "a pedestrian", MotionSubjectKind.Pedestrian),
        new("một học sinh đi bộ", "a student walking", MotionSubjectKind.Pedestrian),
        new("một vận động viên chạy bộ", "a runner", MotionSubjectKind.Runner),
        new("một người chạy bộ", "a jogger", MotionSubjectKind.Runner)
    ];

    private static readonly string[] WatercraftVi =
    [
        "một chiếc thuyền",
        "một ca nô",
        "một xuồng máy"
    ];

    private static readonly string[] WatercraftEn =
    [
        "a boat",
        "a canoe",
        "a motorboat"
    ];

    private readonly Random _random;

    public MotionQuizGenerator(Random? random = null)
    {
        _random = random ?? Random.Shared;
    }

    public ArithmeticQuizQuestion GenerateAlgorithm(
        ArithmeticQuizMode mode,
        AppLanguage language,
        MotionQuizType? requestedType = null) =>
        CreateQuestion(mode, CreateContract(language, requestedType));

    public ArithmeticQuizQuestion GenerateContract(
        ArithmeticQuizMode mode,
        AppLanguage language,
        MotionQuizType? requestedType = null) =>
        CreateQuestion(mode, CreateContract(language, requestedType));

    private MotionQuizContract CreateContract(
        AppLanguage language,
        MotionQuizType? requestedType)
    {
        MotionQuizType type =
            requestedType ?? (MotionQuizType)_random.Next(4);

        return type switch
        {
            MotionQuizType.Basic => CreateBasicContract(language),
            MotionQuizType.Chasing => CreateChasingContract(language),
            MotionQuizType.Meeting => CreateMeetingContract(language),
            MotionQuizType.River => CreateRiverContract(language),
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };
    }

    private MotionQuizContract CreateBasicContract(AppLanguage language)
    {
        MotionQuestionKind kind = _random.Next(4) switch
        {
            0 => MotionQuestionKind.BasicDistance,
            1 => MotionQuestionKind.BasicSpeed,
            2 => MotionQuestionKind.BasicTime,
            _ => MotionQuestionKind.BasicRestDistance
        };

        (MovingSubject movingSubject, MotionUnitProfile profile) =
            PickMovingSubjectAndProfile(language, kind == MotionQuestionKind.BasicRestDistance);
        string subject = GetSubjectText(movingSubject, language);
        string speedUnit = GetSpeedUnit(profile, language);
        string timeUnit = GetTimeUnit(profile, language);
        string distanceUnit = GetDistanceUnit(profile, language);

        if (kind == MotionQuestionKind.BasicRestDistance)
        {
            // Giữ dạng nghỉ dễ hiểu: giờ/km-h hoặc giây/m-s; không dùng phút
            // để tránh phải đưa thêm dữ kiện quy đổi không cần thiết.
            // Profile đã được chọn theo chính đối tượng ở trên; rest-only chỉ
            // loại profile phút chứ không thay sang đơn vị không phù hợp.
            speedUnit = GetSpeedUnit(profile, language);
            timeUnit = GetTimeUnit(profile, language);
            distanceUnit = GetDistanceUnit(profile, language);

            int speed = PickRealisticSpeed(movingSubject.Kind, profile);
            int travelTime = profile.TimeUnitEn == "seconds"
                ? _random.Next(10, 61)
                : _random.Next(1, 5);
            int restTime = _random.Next(1, travelTime + 1);
            int totalElapsed = travelTime + restTime;
            int distance = checked(speed * travelTime * profile.DistanceScale);

            string problem = language == AppLanguage.Vietnamese
                ? $"{Capitalize(subject)} đi với vận tốc {speed} {speedUnit}. Tổng thời gian từ lúc xuất phát đến lúc đến nơi là {totalElapsed} {timeUnit}, trong đó nghỉ {restTime} {timeUnit}. Hỏi {subject} đi được quãng đường bao nhiêu {distanceUnit}?"
                : $"{Capitalize(subject)} travels at {speed} {speedUnit}. The total elapsed time is {totalElapsed} {timeUnit}, including a rest of {restTime} {timeUnit}. How far does {subject} actually travel in {distanceUnit}?";

            string equation =
                $"({totalElapsed} - {restTime}) × {speed} × {profile.DistanceScale} = {distance}";
            string solution = language == AppLanguage.Vietnamese
                ? $"Thời gian thực đi: {totalElapsed} - {restTime} = {travelTime} {timeUnit}{Environment.NewLine}" +
                  $"Quãng đường: {travelTime} × {speed} × {profile.DistanceScale} = {distance} {distanceUnit}"
                : $"Actual travel time: {totalElapsed} - {restTime} = {travelTime} {timeUnit}{Environment.NewLine}" +
                  $"Distance: {travelTime} × {speed} × {profile.DistanceScale} = {distance} {distanceUnit}";

            return new(
                MotionQuizType.Basic,
                kind,
                [speed, totalElapsed, restTime],
                distance,
                distanceUnit,
                language == AppLanguage.Vietnamese ? "quãng đường" : "distance",
                problem,
                equation,
                solution,
                travelTime,
                ArithmeticOperation.Multiply,
                speed * profile.DistanceScale,
                DistinctUnits(speedUnit, timeUnit, distanceUnit));
        }

        if (kind == MotionQuestionKind.BasicDistance)
        {
            (int speed, int time, int distance) = CreateSpeedTimeDistance(profile, movingSubject.Kind);
            string problem = language == AppLanguage.Vietnamese
                ? $"{Capitalize(subject)} đi đều với vận tốc {speed} {speedUnit} trong {time} {timeUnit}. Hỏi quãng đường đi được là bao nhiêu {distanceUnit}?"
                : $"{Capitalize(subject)} moves at a constant speed of {speed} {speedUnit} for {time} {timeUnit}. How far does it travel in {distanceUnit}?";
            string equation = profile.TimeDivisor == 1
                ? $"{speed} × {time} × {profile.DistanceScale} = {distance}"
                : $"{speed} × {time} ÷ {profile.TimeDivisor} = {distance}";
            string solution = language == AppLanguage.Vietnamese
                ? $"Quãng đường: {equation} {distanceUnit}"
                : $"Distance: {equation} {distanceUnit}";

            return new(
                MotionQuizType.Basic,
                kind,
                [speed, time],
                distance,
                distanceUnit,
                language == AppLanguage.Vietnamese ? "quãng đường" : "distance",
                problem,
                equation,
                solution,
                profile.TimeDivisor == 1 ? speed : speed * time,
                profile.TimeDivisor == 1 ? ArithmeticOperation.Multiply : ArithmeticOperation.Divide,
                profile.TimeDivisor == 1 ? time * profile.DistanceScale : profile.TimeDivisor,
                DistinctUnits(speedUnit, timeUnit, distanceUnit));
        }

        if (kind == MotionQuestionKind.BasicSpeed)
        {
            (int speed, int time, int distance) = CreateSpeedTimeDistance(profile, movingSubject.Kind);
            string problem = language == AppLanguage.Vietnamese
                ? $"{Capitalize(subject)} đi được {distance} {distanceUnit} trong {time} {timeUnit}. Hỏi vận tốc của {subject} là bao nhiêu {speedUnit}?"
                : $"{Capitalize(subject)} travels {distance} {distanceUnit} in {time} {timeUnit}. What is its speed in {speedUnit}?";
            int normalizedDistance = distance / profile.DistanceScale;
            int numerator = checked(normalizedDistance * profile.TimeDivisor);
            string equation = $"{distance} ÷ {profile.DistanceScale} × {profile.TimeDivisor} ÷ {time} = {speed}";
            string solution = language == AppLanguage.Vietnamese
                ? $"Đổi quãng đường về đơn vị phù hợp rồi tính vận tốc:{Environment.NewLine}{equation} {speedUnit}"
                : $"Convert the distance to the matching unit, then find speed:{Environment.NewLine}{equation} {speedUnit}";

            return new(
                MotionQuizType.Basic,
                kind,
                [distance, time],
                speed,
                speedUnit,
                language == AppLanguage.Vietnamese ? "vận tốc" : "speed",
                problem,
                equation,
                solution,
                numerator,
                ArithmeticOperation.Divide,
                time,
                DistinctUnits(distanceUnit, timeUnit, speedUnit));
        }

        // BasicTime
        (int targetSpeed, int targetTime, int targetDistance) =
            CreateSpeedTimeDistance(profile, movingSubject.Kind);
        string timeProblem = language == AppLanguage.Vietnamese
            ? $"{Capitalize(subject)} đi đều với vận tốc {targetSpeed} {speedUnit} và đi được {targetDistance} {distanceUnit}. Hỏi {subject} đi trong bao nhiêu {timeUnit}?"
            : $"{Capitalize(subject)} moves at {targetSpeed} {speedUnit} and covers {targetDistance} {distanceUnit}. How many {timeUnit} does it travel?";
        int normalizedTargetDistance = targetDistance / profile.DistanceScale;
        int timeNumerator = checked(normalizedTargetDistance * profile.TimeDivisor);
        string timeEquation = $"{targetDistance} ÷ {profile.DistanceScale} × {profile.TimeDivisor} ÷ {targetSpeed} = {targetTime}";
        string timeSolution = language == AppLanguage.Vietnamese
            ? $"Thời gian: {timeEquation} {timeUnit}"
            : $"Time: {timeEquation} {timeUnit}";

        return new(
            MotionQuizType.Basic,
            kind,
            [targetSpeed, targetDistance],
            targetTime,
            timeUnit,
            language == AppLanguage.Vietnamese ? "thời gian" : "time",
            timeProblem,
            timeEquation,
            timeSolution,
            timeNumerator,
            ArithmeticOperation.Divide,
            targetSpeed,
            DistinctUnits(speedUnit, distanceUnit, timeUnit));
    }

    private MotionQuizContract CreateChasingContract(AppLanguage language)
    {
        (MovingSubject slowMovingSubject, MovingSubject fastMovingSubject, MotionUnitProfile profile) =
            PickMovingSubjectPairAndProfile(language);
        string slowSubject = GetSubjectText(slowMovingSubject, language);
        string fastSubject = GetSubjectText(fastMovingSubject, language);

        string speedUnit = GetSpeedUnit(profile, language);
        string timeUnit = GetTimeUnit(profile, language);
        string distanceUnit = GetDistanceUnit(profile, language);

        (int slowSpeed, int fastSpeed, int time, int gap) =
            CreateChasingNumbers(profile, slowMovingSubject.Kind);

        string problem = language == AppLanguage.Vietnamese
            ? $"{Capitalize(slowSubject)} đi trước và đang cách {fastSubject} {gap} {distanceUnit}. {Capitalize(slowSubject)} đi với vận tốc {slowSpeed} {speedUnit}, còn {fastSubject} đi cùng chiều với vận tốc {fastSpeed} {speedUnit}. Hỏi sau bao nhiêu {timeUnit} thì {fastSubject} đuổi kịp?"
            : $"{Capitalize(slowSubject)} is {gap} {distanceUnit} ahead of {fastSubject}. {Capitalize(slowSubject)} moves at {slowSpeed} {speedUnit}, while {fastSubject} moves in the same direction at {fastSpeed} {speedUnit}. After how many {timeUnit} will the faster one catch up?";

        int relativeSpeed = fastSpeed - slowSpeed;
        string equation = profile.TimeDivisor == 1
            ? $"{gap} ÷ {profile.DistanceScale} ÷ ({fastSpeed} - {slowSpeed}) = {time}"
            : $"{gap} × {profile.TimeDivisor} ÷ ({fastSpeed} - {slowSpeed}) = {time}";
        string solution = language == AppLanguage.Vietnamese
            ? $"Hiệu vận tốc: {fastSpeed} - {slowSpeed} = {relativeSpeed} {speedUnit}{Environment.NewLine}" +
              $"Thời gian đuổi kịp: {equation} {timeUnit}"
            : $"Relative speed: {fastSpeed} - {slowSpeed} = {relativeSpeed} {speedUnit}{Environment.NewLine}" +
              $"Catch-up time: {equation} {timeUnit}";

        BigInteger numerator = profile.TimeDivisor == 1
            ? gap / profile.DistanceScale
            : (BigInteger)gap * profile.TimeDivisor;

        return new(
            MotionQuizType.Chasing,
            MotionQuestionKind.CatchUpTime,
            [gap, slowSpeed, fastSpeed],
            time,
            timeUnit,
            language == AppLanguage.Vietnamese ? "thời gian đuổi kịp" : "catch-up time",
            problem,
            equation,
            solution,
            numerator,
            ArithmeticOperation.Divide,
            relativeSpeed,
            DistinctUnits(distanceUnit, speedUnit, timeUnit));
    }

    private MotionQuizContract CreateMeetingContract(AppLanguage language)
    {
        (MovingSubject movingSubject1, MovingSubject movingSubject2, MotionUnitProfile profile) =
            PickMovingSubjectPairAndProfile(language);
        string subject1 = GetSubjectText(movingSubject1, language);
        string subject2 = GetSubjectText(movingSubject2, language);

        string speedUnit = GetSpeedUnit(profile, language);
        string timeUnit = GetTimeUnit(profile, language);
        string distanceUnit = GetDistanceUnit(profile, language);

        (int speed1, int speed2, int time, int distance) =
            CreateMeetingNumbers(profile, movingSubject1.Kind);

        string problem = language == AppLanguage.Vietnamese
            ? $"{Capitalize(subject1)} và {subject2} ở hai điểm cách nhau {distance} {distanceUnit}, cùng lúc đi ngược chiều về phía nhau. Vận tốc lần lượt là {speed1} {speedUnit} và {speed2} {speedUnit}. Hỏi sau bao nhiêu {timeUnit} thì hai bên gặp nhau?"
            : $"{Capitalize(subject1)} and {subject2} start {distance} {distanceUnit} apart and move toward each other at the same time. Their speeds are {speed1} {speedUnit} and {speed2} {speedUnit}. After how many {timeUnit} will they meet?";

        int relativeSpeed = speed1 + speed2;
        string equation = profile.TimeDivisor == 1
            ? $"{distance} ÷ {profile.DistanceScale} ÷ ({speed1} + {speed2}) = {time}"
            : $"{distance} × {profile.TimeDivisor} ÷ ({speed1} + {speed2}) = {time}";
        string solution = language == AppLanguage.Vietnamese
            ? $"Tổng vận tốc: {speed1} + {speed2} = {relativeSpeed} {speedUnit}{Environment.NewLine}" +
              $"Thời gian gặp nhau: {equation} {timeUnit}"
            : $"Combined speed: {speed1} + {speed2} = {relativeSpeed} {speedUnit}{Environment.NewLine}" +
              $"Meeting time: {equation} {timeUnit}";

        BigInteger numerator = profile.TimeDivisor == 1
            ? distance / profile.DistanceScale
            : (BigInteger)distance * profile.TimeDivisor;

        return new(
            MotionQuizType.Meeting,
            MotionQuestionKind.MeetingTime,
            [distance, speed1, speed2],
            time,
            timeUnit,
            language == AppLanguage.Vietnamese ? "thời gian gặp nhau" : "meeting time",
            problem,
            equation,
            solution,
            numerator,
            ArithmeticOperation.Divide,
            relativeSpeed,
            DistinctUnits(distanceUnit, speedUnit, timeUnit));
    }

    private MotionQuizContract CreateRiverContract(AppLanguage language)
    {
        MotionQuestionKind kind = _random.Next(4) switch
        {
            0 => MotionQuestionKind.RiverDownstreamSpeed,
            1 => MotionQuestionKind.RiverUpstreamSpeed,
            2 => MotionQuestionKind.RiverBoatSpeed,
            _ => MotionQuestionKind.RiverCurrentSpeed
        };

        MotionUnitProfile profile = PickRiverProfile(language);
        string craft = PickWatercraft(language);
        string speedUnit = GetSpeedUnit(profile, language);
        int boatSpeed = profile.SpeedUnitEn == "m/s"
            ? _random.Next(5, 16)
            : profile.SpeedUnitEn == "mph"
                ? _random.Next(15, 41)
                : _random.Next(18, 46);
        int currentSpeed = profile.SpeedUnitEn == "m/s"
            ? _random.Next(1, Math.Min(5, boatSpeed))
            : _random.Next(2, Math.Min(11, boatSpeed));
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
        string problem = language == AppLanguage.Vietnamese
            ? $"{Capitalize(craft)} có vận tốc khi nước yên là {boatSpeed} {speedUnit}, vận tốc dòng nước là {currentSpeed} {speedUnit}. Hỏi vận tốc {(isDownstream ? "xuôi dòng" : "ngược dòng")} của {craft} là bao nhiêu {speedUnit}?"
            : $"{Capitalize(craft)} moves at {boatSpeed} {speedUnit} in still water, and the current speed is {currentSpeed} {speedUnit}. What is its {(isDownstream ? "downstream" : "upstream")} speed in {speedUnit}?";
        string equation = isDownstream
            ? $"{boatSpeed} + {currentSpeed} = {answer}"
            : $"{boatSpeed} - {currentSpeed} = {answer}";
        string solution = language == AppLanguage.Vietnamese
            ? $"Vận tốc {(isDownstream ? "xuôi dòng" : "ngược dòng")}: {equation} {speedUnit}"
            : $"{(isDownstream ? "Downstream" : "Upstream")} speed: {equation} {speedUnit}";

        return new(
            MotionQuizType.River,
            kind,
            [boatSpeed, currentSpeed],
            answer,
            speedUnit,
            language == AppLanguage.Vietnamese
                ? isDownstream ? "vận tốc xuôi dòng" : "vận tốc ngược dòng"
                : isDownstream ? "downstream speed" : "upstream speed",
            problem,
            equation,
            solution,
            boatSpeed,
            isDownstream ? ArithmeticOperation.Add : ArithmeticOperation.Subtract,
            currentSpeed,
            DistinctUnits(speedUnit));
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
        string problem = language == AppLanguage.Vietnamese
            ? $"{Capitalize(craft)} có vận tốc xuôi dòng {downstream} {speedUnit} và vận tốc ngược dòng {upstream} {speedUnit}. Hỏi {(findBoat ? "vận tốc của thuyền khi nước yên" : "vận tốc dòng nước")} là bao nhiêu {speedUnit}?"
            : $"{Capitalize(craft)} has a downstream speed of {downstream} {speedUnit} and an upstream speed of {upstream} {speedUnit}. What is the {(findBoat ? "speed in still water" : "current speed")} in {speedUnit}?";
        int numerator = findBoat
            ? downstream + upstream
            : downstream - upstream;
        string equation = findBoat
            ? $"({downstream} + {upstream}) ÷ 2 = {answer}"
            : $"({downstream} - {upstream}) ÷ 2 = {answer}";
        string solution = language == AppLanguage.Vietnamese
            ? $"{(findBoat ? "Vận tốc thuyền khi nước yên" : "Vận tốc dòng nước")}: {equation} {speedUnit}"
            : $"{(findBoat ? "Still-water speed" : "Current speed")}: {equation} {speedUnit}";

        return new(
            MotionQuizType.River,
            kind,
            [downstream, upstream],
            answer,
            speedUnit,
            language == AppLanguage.Vietnamese
                ? findBoat ? "vận tốc thuyền" : "vận tốc dòng nước"
                : findBoat ? "boat speed" : "current speed",
            problem,
            equation,
            solution,
            numerator,
            ArithmeticOperation.Divide,
            2,
            DistinctUnits(speedUnit));
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
            MotionSubjectKind subjectKind)
    {
        for (int attempt = 0; attempt < 128; attempt++)
        {
            int speed = PickRealisticSpeed(subjectKind, profile);
            int time = profile.TimeDivisor switch
            {
                60 => Pick(new[] { 10, 15, 20, 30, 45, 60, 90, 120 }),
                _ when profile.TimeUnitEn == "seconds" => _random.Next(5, 61),
                _ => _random.Next(1, 6)
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
            MotionSubjectKind subjectKind)
    {
        for (int attempt = 0; attempt < 128; attempt++)
        {
            int time = PickTimeForRelativeProfile(profile);
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
            MotionSubjectKind subjectKind)
    {
        for (int attempt = 0; attempt < 128; attempt++)
        {
            int time = PickTimeForRelativeProfile(profile);
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

    private int PickTimeForRelativeProfile(MotionUnitProfile profile) =>
        profile.TimeDivisor == 60
            ? Pick(new[] { 10, 15, 20, 30, 45, 60 })
            : profile.TimeUnitEn == "seconds"
                ? _random.Next(5, 61)
                : _random.Next(1, 5);

    private (MovingSubject Subject, MotionUnitProfile Profile)
        PickMovingSubjectAndProfile(
            AppLanguage language,
            bool restOnly)
    {
        MovingSubject subject = Pick(MovingSubjects);
        MotionUnitProfile profile = PickProfileForSubject(
            subject.Kind,
            language,
            restOnly);
        return (subject, profile);
    }

    private (MovingSubject First, MovingSubject Second, MotionUnitProfile Profile)
        PickMovingSubjectPairAndProfile(AppLanguage language)
    {
        MotionSubjectKind[] pairKinds = MovingSubjects
            .GroupBy(subject => subject.Kind)
            .Where(group => group.Count() >= 2)
            .Select(group => group.Key)
            .ToArray();

        MotionSubjectKind kind = Pick(pairKinds);
        MovingSubject[] candidates = MovingSubjects
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
            restOnly: false);

        return (first, second, profile);
    }

    private MotionUnitProfile PickProfileForSubject(
        MotionSubjectKind kind,
        AppLanguage language,
        bool restOnly)
    {
        MotionUnitKind[] allowedKinds = kind switch
        {
            MotionSubjectKind.MotorVehicle =>
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

        MotionUnitProfile[] candidates = UnitProfiles
            .Where(profile =>
                allowedKinds.Contains(profile.Kind) &&
                (!profile.EnglishOnly || language == AppLanguage.English) &&
                (!restOnly || profile.TimeDivisor == 1))
            .ToArray();

        return Pick(candidates);
    }

    private MotionUnitProfile PickRiverProfile(AppLanguage language)
    {
        MotionUnitKind[] allowedKinds = language == AppLanguage.English
            ? [MotionUnitKind.RoadKmHour, MotionUnitKind.MeterSecond, MotionUnitKind.MilesHour]
            : [MotionUnitKind.RoadKmHour, MotionUnitKind.MeterSecond];

        MotionUnitProfile[] candidates = UnitProfiles
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
            (MotionSubjectKind.MotorVehicle, MotionUnitKind.MilesHour) => (15, 61),
            (MotionSubjectKind.Bicycle, MotionUnitKind.RoadKmHour) => (8, 31),
            (MotionSubjectKind.Bicycle, MotionUnitKind.MeterSecond) => (2, 9),
            (MotionSubjectKind.Bicycle, MotionUnitKind.MilesHour) => (5, 20),
            (MotionSubjectKind.FastAnimal, MotionUnitKind.RoadKmHour) => (15, 61),
            (MotionSubjectKind.FastAnimal, MotionUnitKind.MeterSecond) => (4, 16),
            (MotionSubjectKind.FastAnimal, MotionUnitKind.MilesHour) => (10, 36),
            (MotionSubjectKind.MediumAnimal, MotionUnitKind.MeterSecond) => (2, 11),
            (MotionSubjectKind.TinyAnimal, MotionUnitKind.CentimeterSecond) => (1, 9),
            (MotionSubjectKind.TinyAnimal, MotionUnitKind.MillimeterSecond) => (2, 21),
            (MotionSubjectKind.Pedestrian, MotionUnitKind.MeterSecond) => (1, 4),
            (MotionSubjectKind.Runner, MotionUnitKind.MeterSecond) => (2, 9),
            _ => (2, 12)
        };

    private static string GetSubjectText(
        MovingSubject subject,
        AppLanguage language) =>
        language == AppLanguage.Vietnamese
            ? subject.Vietnamese
            : subject.English;

    private string PickWatercraft(AppLanguage language) =>
        language == AppLanguage.Vietnamese
            ? Pick(WatercraftVi)
            : Pick(WatercraftEn);

    private T Pick<T>(IReadOnlyList<T> values) =>
        values[_random.Next(values.Count)];

    private static IReadOnlyList<string> DistinctUnits(params string[] units) =>
        units
            .Where(unit => !string.IsNullOrWhiteSpace(unit))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static string GetSpeedUnit(MotionUnitProfile profile, AppLanguage language) =>
        language == AppLanguage.Vietnamese ? profile.SpeedUnitVi : profile.SpeedUnitEn;

    private static string GetTimeUnit(MotionUnitProfile profile, AppLanguage language) =>
        language == AppLanguage.Vietnamese ? profile.TimeUnitVi : profile.TimeUnitEn;

    private static string GetDistanceUnit(MotionUnitProfile profile, AppLanguage language) =>
        language == AppLanguage.Vietnamese ? profile.DistanceUnitVi : profile.DistanceUnitEn;

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
