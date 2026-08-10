public interface IPlayer
{
    void OnTurnStarted();           
    void OnGameStarted();           
    bool IsHuman { get; }           
}