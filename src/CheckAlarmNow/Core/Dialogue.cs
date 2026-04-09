namespace CheckAlarmNow.Core;

public static class Dialogue
{
    private static readonly Random Rand = new();

    private static readonly string[] IdleLines =
    { "zzZ...", "쿨쿨", "자는 중..." };

    private static readonly string[] WarnLines =
    { "알림 확인해주세요~", "알림 왔어요", "확인 좀요~" };

    private static readonly string[] AlertLines =
    { "지금 확인하세요!", "알림 안 볼 거야?!", "무시하면 안 돼요!!" };

    private static readonly string[] HappyLines =
    { "확인 완료!", "잘했어요!", "다시 잘게~" };

    private static string Pick(string[] lines) => lines[Rand.Next(lines.Length)];

    public static string GetLine(PetState state, double annoyance)
    {
        return state switch
        {
            PetState.Idle => Pick(IdleLines),
            PetState.Warn => Pick(WarnLines),
            PetState.Alert => Pick(AlertLines),
            PetState.Happy => Pick(HappyLines),
            _ => "..."
        };
    }
}
