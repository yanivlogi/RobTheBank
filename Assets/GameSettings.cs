public static class GameSettings
{
    public static int PointsToWin = 10;
    public static int TurnTimeSeconds = 60;
    public static bool FriendlyRobber = false;
    public static bool AllowTrade = true;
    public static int MaxPlayers = 4;
    public static string RoomName = "Catan Room";
    public static string HostPlayerName = "Player";

    public static void Reset()
    {
        PointsToWin = 10;
        TurnTimeSeconds = 60;
        FriendlyRobber = false;
        AllowTrade = true;
        MaxPlayers = 4;
        RoomName = "Catan Room";
        HostPlayerName = "Player";
    }
}
