using System.Collections;
using UnityEngine;
using UnityEngine.Playables;

namespace com.tinylabproductions.TLPLib.Extensions {
  public static class PlayableDirectorExts {
    
    public static IEnumerator play(this PlayableDirector director, PlayableAsset asset) {
      director.Play(asset, DirectorWrapMode.Hold);
      director.Evaluate();
      while (!Mathf.Approximately((float) director.time, (float) asset.duration))
        yield return null;
    }
    
    public static IEnumerator play(this PlayableDirector director) {
       yield return director.play(director.playableAsset);
    }
    
    public static void setInitial(this PlayableDirector director, PlayableAsset asset) {
      director.Play(asset, DirectorWrapMode.Hold);
      director.Evaluate();
      director.Stop();
    }
    
  }
}