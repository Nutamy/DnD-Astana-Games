using UnityEngine;

public class GameStateController : MonoBehaviour
{
    public static GameStateController Instance { get; private set; }
    public static GameState CurrentState => Instance._currentState;
    
    private GameState _currentState = GameState.Default;
    public enum GameState
    {
        Default,
        ChatOpen,
        SettingsMenu,
        AbilityCasting
    }
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    /// <summary>
    /// Change the current state of the game
    /// </summary>
    /// <param name="state"></param>
    public void SetState(GameState state)
    {
        _currentState = state;
    }
}