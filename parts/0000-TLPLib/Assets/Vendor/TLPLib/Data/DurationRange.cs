using System;
using com.tinylabproductions.TLPLib.Configuration;
using com.tinylabproductions.TLPLib.Data.typeclasses;
using com.tinylabproductions.TLPLib.Functional;
using GenerationAttributes;
using JetBrains.Annotations;
using UnityEngine;

namespace com.tinylabproductions.TLPLib.Data {
  [Serializable, Record, PublicAPI] public partial struct DurationRange : IStr {
    [SerializeField, PublicAccessor] Duration _from, _to;

    public string asString() => $"[{_from}..{_to}]";

    public Duration random(ref Rng rng) => 
      new Duration(rng.nextIntInRange(new Range(_from.millis, _to.millis), out rng));

    public static readonly Config.Parser<object, DurationRange> parser =
      Config.immutableArrayParser(Duration.configParser).flatMap((_, cfg) => {
        if (cfg.Length == 2) 
          return Either<ConfigLookupError, DurationRange>.Right(new DurationRange(cfg[0], cfg[1]));
        else
          return ConfigLookupError.fromException(new Exception(
            $"Expected duration range to have 2 elements, but it had {cfg.Length}"
          ));
      });
  }
}