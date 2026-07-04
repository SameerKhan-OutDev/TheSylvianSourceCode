using System;
using System.Collections.Generic;
using UnityEngine;

namespace OutGame
{
    /// <summary>
    /// Contract for all objects responsive to aiming and telekinetic/neural manipulation.
    /// </summary>
    public interface IOutInteractable
    {
        EOutInteractableState CurrentState { get; }

        List<Renderer> RendererObjects { get; }

        void OnAimEnter()
        {
            Debug.Log("Aiming at interactable object.");
        }
        void OnAimExit()
        {
            Debug.Log("Stopped aiming at interactable object.");
        }

        /// <summary>
        /// Executes the primary interaction asynchronously.
        /// </summary>
        Awaitable ExecuteInteractionAsync(Transform a_instigator, Action<float> a_onProgress = null);
    }
}