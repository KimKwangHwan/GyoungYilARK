// 역할: 이번 게임에서 지금이 낮인지 밤인지를 한 번만 보관하는 장부
// 쓰는 곳: EnviromentManager가 전환 완료 시점에 현재 환경 상태를 기록함
// 안 하는 일: 낮/밤을 직접 판단하거나 전환시키지 않음
public class DayNightData
{
    private bool isNight;

    public bool IsNight => isNight;

    public void Keep(bool value)
    {
        isNight = value;
    }
}
