using System.Collections.Generic;
using UnityEngine;

namespace OutGame
{
    public interface INonPlayableCharacter
    {
        public List<Transform> Destinations { get; set; }

        public bool followDestinations { get; set; }

        public void FollowDestinations(List<Transform> destinations);
    }
}
