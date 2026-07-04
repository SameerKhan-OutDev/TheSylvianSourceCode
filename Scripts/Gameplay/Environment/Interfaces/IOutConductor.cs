using UnityEngine;

namespace OutGame
{
    /// <summary>
    /// Interface for any living entity that can conduct the spark trap current.
    /// </summary>
    public interface IOutConductor
    {
        /// <summary>
        /// Called by the spark trap when the entity walks into it.
        /// </summary>
        /// <param name="attachPoint">The transform where the victim should be snapped to.</param>
        void OnElectrocuted(Transform attachPoint);
    }
}