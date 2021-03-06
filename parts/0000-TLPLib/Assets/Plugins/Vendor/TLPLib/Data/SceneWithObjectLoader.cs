﻿using com.tinylabproductions.TLPLib.Concurrent;
using com.tinylabproductions.TLPLib.Data.scenes;
using com.tinylabproductions.TLPLib.Extensions;
using com.tinylabproductions.TLPLib.Functional;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace com.tinylabproductions.TLPLib.Data {
  public static class SceneWithObjectLoader {
    public static Future<Either<ErrorMsg, A>> load<A>(
      ScenePath scenePath, LoadSceneMode loadSceneMode = LoadSceneMode.Single
    ) where A : Component =>
      Future.successful(
        F.doTry(() => SceneManager.LoadSceneAsync(scenePath, loadSceneMode))
          .toEitherStr.mapLeft(err => new ErrorMsg($"Error while loading scene '{scenePath}': {err}"))
      ).flatMapT(op => op.toFuture().map(_ =>
        SceneManager.GetSceneByPath(scenePath).findComponentOnRootGameObjects<A>()
      ));
  }
}