using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Manages the sequential display of splash screens, logos, and disclaimer panels,
/// with support for nesting and precise timing controls before transitioning scenes.
/// </summary>
public class OutSplashScreenController : MonoBehaviour
{
    #region Structures

    [Serializable]
    public struct SplashObject
    {
        [Tooltip("The main panel or sub-panel GameObject to control.")]
        public GameObject obj;
        [Tooltip("Delay before this specific panel activates.")]
        public float displayDelay;
        [Tooltip("How long this panel remains visible before turning off.")]
        public float displayTime;

        [Header("Nested Flow")]
        [Tooltip("Optional child sequences that run sequentially while this parent panel is active.")]
        public List<SplashObject> subPanels;
    }

    #endregion

    #region Inspector Fields

    [Header("Sequence Configuration")]
    [SerializeField] private List<SplashObject> splashObjects;

    [Header("Scene Transition")]
    [SerializeField] private float sceneChangeDelay = 1.0f;
    [SerializeField] private string nextSceneName = "MainMenu";

    #endregion

    #region Unity Lifecycle

    private void Start()
    {
        InitializeSequence(splashObjects);
        StartCoroutine(PlaySplashSequence());
    }

    #endregion

    #region Initialization Logic

    private void InitializeSequence(List<SplashObject> targetList)
    {
        if (targetList == null && targetList.Count == 0) return;

        foreach (var splash in targetList)
        {
            if (splash.obj != null)
            {
                splash.obj.SetActive(false);
            }

            if (splash.subPanels != null && splash.subPanels.Count > 0)
            {
                InitializeSequence(splash.subPanels);
            }
        }
    }

    #endregion

    #region Coroutines

    private IEnumerator PlaySplashSequence()
    {
        yield return StartCoroutine(ExecuteListSequence(splashObjects));

        yield return new WaitForSeconds(sceneChangeDelay);
        ChangeSceneNow();
    }

    private IEnumerator ExecuteListSequence(List<SplashObject> targetList)
    {
        if (targetList == null) yield break;

        foreach (var splash in targetList)
        {
            if (splash.obj == null) continue;

            yield return new WaitForSeconds(splash.displayDelay);
            splash.obj.SetActive(true);

            if (splash.subPanels != null && splash.subPanels.Count > 0)
            {
                // Run child panels in sequence inside the parent window frame
                yield return StartCoroutine(ExecuteListSequence(splash.subPanels));
            }
            else
            {
                // Normal display hang time if no inner children are present
                yield return new WaitForSeconds(splash.displayTime);
            }

            splash.obj.SetActive(false);
        }
    }

    #endregion

    #region Scene Management

    private void ChangeSceneNow()
    {
        SceneManager.LoadScene(nextSceneName);
    }

    #endregion
}