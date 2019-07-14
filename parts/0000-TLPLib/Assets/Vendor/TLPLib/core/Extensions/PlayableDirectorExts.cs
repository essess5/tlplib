using System.Collections;
using com.tinylabproductions.TLPLib.Logger;
using UnityEngine;
using UnityEngine.Playables;

namespace com.tinylabproductions.TLPLib.Extensions {
  public static class PlayableDirectorExts {
    public static IEnumerator play(
      this PlayableDirector director, PlayableAsset asset, bool logErrorIfInactive = true
    ) {
      // Directors do not play if the game object is not active.
      if (!director.isActiveAndEnabled) {
        if (logErrorIfInactive) Log.d.error(
          $"Wanted to play {asset} on director, which is not active and enabled! " +
          "This does not work, ensure the director is active and enabled.",
          director
        );
      }
      else {
        director.gameObject.SetActive(true);
        director.enabled = true;
        director.Play(asset, DirectorWrapMode.Hold);
        director.Evaluate();
        while (!Mathf.Approximately((float) director.time, (float) asset.duration))
          yield return null;
      }
    }
    
    
    public static IEnumerator play(this PlayableDirector director) {
       yield return director.play(director.playableAsset);
    }
    
    public static void setInitial(this PlayableDirector director, PlayableAsset asset, bool logErrorIfInactive = true) {
      if (!director.isActiveAndEnabled) {
        if (logErrorIfInactive) Log.d.error(
          $"Wanted to set initial state for {asset} on director, which is not active and enabled! " +
          "This does not work, ensure the director is active and enabled.",
          director
        );
      }
      director.Play(asset, DirectorWrapMode.Hold);
      director.Evaluate();
      director.Stop();
    }
  }
}