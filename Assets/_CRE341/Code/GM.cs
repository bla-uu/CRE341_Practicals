using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

/// <summary>
/// Manages the game's state and scene transitions. Implements the Singleton pattern.
/// </summary>
public class GM : MonoBehaviour
{
    // Singleton instance
    public static GM Inst { get; private set; }

    #region Game State Management
    /// <summary>
    /// Enum defining the possible states of the game.
    /// </summary>
    public enum GameState
    {
        MainMenu,
        GameStart,
        Level_1,
        Level_2,
        Level_3,
        Paused,
        GameOver
    }

    /// <summary>
    /// The current state of the game.
    /// </summary>
    public GameState currentState { get; private set; }
    #endregion

    private void Awake()
    {
        // Singleton pattern implementation
        if (Inst == null)
        {
            Inst = this;
            DontDestroyOnLoad(gameObject); // Persist across scene loads
        }
        else
        {
            Destroy(gameObject);
            return; // Prevent further execution if another instance exists
        }

        // Initialize the game state
        currentState = GameState.Level_1;
    }

    #region Scene Management Methods
    /// <summary>
    /// Changes the current game state and loads the corresponding scene.
    /// </summary>
    /// <param name="newState">The new game state to transition to.</param>
    /// <param name="sceneName">The name of the scene to load (optional).</param>
    public void ChangeState(GameState newState, string sceneName = "")
    {
        currentState = newState;

        switch (currentState)
        {
            case GameState.MainMenu:
                LoadScene(sceneName);
                break;

            case GameState.GameStart:
                // Handle any GameStart specific logic here
                break;

            case GameState.Level_1:
                LoadScene(sceneName);
                break;

            case GameState.Paused:
                PauseGame();
                break;

            case GameState.GameOver:
                LoadScene(sceneName);
                break;

            default:
                Debug.LogError("Invalid game state specified!");
                break;
        }
    }

    /// <summary>
    /// Starts the game by transitioning to the GameStart state and loading the first level.
    /// </summary>
    /// <param name="gameplaySceneName">The name of the first level scene.</param>
    public void StartGame(string gameplaySceneName)
    {
        ChangeState(GameState.GameStart);
        StartCoroutine(LoadSceneAndSwitchState(gameplaySceneName, GameState.Level_1));
    }

    /// <summary>
    /// Pauses the game by setting the time scale to zero.
    /// </summary>
    public void PauseGame()
    {
        Time.timeScale = 0;
    }

    /// <summary>
    /// Unpauses the game by setting the time scale to one.
    /// </summary>
    public void UnpauseGame()
    {
        Time.timeScale = 1;
    }

    /// <summary>
    /// Navigates to the main menu scene.
    /// </summary>
    /// <param name="mainMenuSceneName">The name of the main menu scene.</param>
    public void GoToMainMenu(string mainMenuSceneName)
    {
        ChangeState(GameState.MainMenu, mainMenuSceneName);
    }

    /// <summary>
    /// Handles the game over state and loads the game over scene.
    /// </summary>
    /// <param name="gameOverSceneName">The name of the game over scene.</param>
    public void GameOver(string gameOverSceneName)
    {
        ChangeState(GameState.GameOver, gameOverSceneName);
    }

    /// <summary>
    /// Loads a scene if the scene name is valid.
    /// </summary>
    /// <param name="sceneName">The name of the scene to load.</param>
    private void LoadScene(string sceneName)
    {
        if (!string.IsNullOrEmpty(sceneName))
        {
            SceneManager.LoadScene(sceneName);
        }
    }

    /// <summary>
    /// Loads a scene asynchronously and then transitions to a new game state.
    /// </summary>
    /// <param name="sceneName">The name of the scene to load.</param>
    /// <param name="newState">The game state to transition to after loading.</param>
    private IEnumerator LoadSceneAndSwitchState(string sceneName, GameState newState)
    {
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName);

        while (!asyncLoad.isDone)
        {
            yield return null;
        }

        ChangeState(newState);
    }
    #endregion
}