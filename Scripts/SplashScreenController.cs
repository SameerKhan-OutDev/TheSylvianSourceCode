using System.Collections.Generic;
using System.Collections;
using System;
using UnityEngine;

public class SplashScreenController : MonoBehaviour
{
    [Serializable]
    public struct SplashObject
    {
        public GameObject obj;
        public float displayDelay;
        public float displayTime;
    }

    public List<SplashObject> splashObjects;

    [Space]
    public float sceneChangeDelay = 1.0f;
    public string nextSceneName = "MainMenu";

    public void Start()
    {
        foreach (var splash in splashObjects)
        {
            splash.obj.SetActive(false);
        }
        StartCoroutine(PlaySplashSequence());
    }

    private IEnumerator PlaySplashSequence()
    {
        foreach (var splash in splashObjects)
        {
            yield return new WaitForSeconds(splash.displayDelay);
            splash.obj.SetActive(true);
            yield return new WaitForSeconds(splash.displayTime);
            splash.obj.SetActive(false);
        }

        Invoke(nameof(ChangeSceneNow), sceneChangeDelay);
    }

    private void ChangeSceneNow()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene(nextSceneName);
    }

}
