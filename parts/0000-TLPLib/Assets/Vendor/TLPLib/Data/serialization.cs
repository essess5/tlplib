﻿using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using com.tinylabproductions.TLPLib.Collection;
using com.tinylabproductions.TLPLib.Data.serialization;
using com.tinylabproductions.TLPLib.Data.typeclasses;
using com.tinylabproductions.TLPLib.Extensions;
using com.tinylabproductions.TLPLib.Filesystem;
using com.tinylabproductions.TLPLib.Functional;
using com.tinylabproductions.TLPLib.Utilities;
using JetBrains.Annotations;
using UnityEngine;
using Object = UnityEngine.Object;

namespace com.tinylabproductions.TLPLib.Data {
  public interface ISerializer<in A> {
    Rope<byte> serialize(A a);
  }

  public struct DeserializeInfo<A> {
    public readonly A value;
    public readonly int bytesRead;

    public DeserializeInfo(A value, int bytesRead) {
      this.value = value;
      this.bytesRead = bytesRead;
    }

    public Either<string, DeserializeInfo<B>> flatMapTry<B>(Func<A, B> mapper) {
      try {
        return new DeserializeInfo<B>(mapper(value), bytesRead);
      }
      catch (Exception e) {
        return $"{nameof(flatMapTry)} from {typeof(A).FullName} to {typeof(B).FullName} threw {e}";
      }
    }

    public DeserializeInfo<B> map<B>(Func<A, B> mapper) => 
      new DeserializeInfo<B>(mapper(value), bytesRead);
  }

  [PublicAPI] public static class DeserializeInfoExts {
    public static Option<DeserializeInfo<B>> map<A, B>(
      this Option<DeserializeInfo<A>> aOpt, Func<A, B> mapper
    ) {
      if (aOpt.isNone) return Option<DeserializeInfo<B>>.None;
      var aInfo = aOpt.get;
      return F.some(new DeserializeInfo<B>(mapper(aInfo.value), aInfo.bytesRead));
    }
  }

  public interface IDeserializer<A> {
    // Returns None if deserialization failed.
    Either<string, DeserializeInfo<A>> deserialize(byte[] serialized, int startIndex);
  }

  // TODO: document
  public interface ISerializedRW<A> : IDeserializer<A>, ISerializer<A> { }

  public delegate Either<string, A> Deserialize<A>(byte[] serialized, int startIndex);

  public delegate Rope<byte> Serialize<in A>(A a);

  [PublicAPI] public static class SerializedRW {
    public static readonly ISerializedRW<string> str = new stringRW();
    public static readonly ISerializedRW<int> integer = new intRW();
    public static readonly ISerializedRW<byte> byte_ = new byteRW();
    [PublicAPI] public static readonly ISerializedRW<byte[]> byteArray = new byteArrayRW();
    [PublicAPI] public static readonly ISerializedRW<uint> uInteger = new uintRW();
    [PublicAPI] public static readonly ISerializedRW<ulong> uLong = new ulongRW();
    [PublicAPI] public static readonly ISerializedRW<ushort> uShort = new ushortRW();
    [PublicAPI] public static readonly ISerializedRW<bool> boolean = new boolRW();
    [PublicAPI] public static readonly ISerializedRW<float> flt = new floatRW();
    [PublicAPI] public static readonly ISerializedRW<long> lng = new longRW();
    [PublicAPI] public static readonly ISerializedRW<DateTime> 
      dateTime = new DateTimeRW(),
      dateTimeMillisTimestamp = new DateTimeMillisTimestampRW();

    [PublicAPI] public static readonly ISerializedRW<Vector2> vector2 =
      flt.and(flt, (x, y) => new Vector2(x, y), _ => _.x, _ => _.y);

    [PublicAPI] public static readonly ISerializedRW<Vector3> vector3 =
      vector2.and(flt, (v2, z) => new Vector3(v2.x, v2.y, z), _ => _, _ => _.z);

    [PublicAPI] public static readonly ISerializedRW<Url> url = 
      str.map(_ => Either<string, Url>.Right(new Url(_)), _ => _.url);
    [PublicAPI] public static readonly ISerializedRW<Uri> uri = lambda(
      uri => str.serialize(uri.ToString()),
      (bytes, startIndex) =>
        str.deserialize(bytes, startIndex).flatMapRight(di => di.flatMapTry(s => new Uri(s)))
    );

    [PublicAPI] public static readonly ISerializedRW<Guid> guid = new GuidRW();

    [PublicAPI] public static readonly ISerializedRW<TextureFormat> textureFormat =
      integer.map(
        i => 
          EnumUtils.GetValues<TextureFormat>().find(_ => (int) _ == i)
          .toRight($"Can't find texture format by {i}"),
        tf => (int) tf
      );

    [PublicAPI] public static readonly ISerializedRW<Color32> color32 =
      BytePair.rw.and(BytePair.rw, 
        (bp1, bp2) => {
          var (r, g) = bp1;
          var (b, a) = bp2;
          return new Color32(r, g, b, a);
        },
        c => new BytePair(c.r, c.g),
        c => new BytePair(c.b, c.a)
      );
    
    // RWs for library or user defined types go as static fields of those types. 

#if UNITY_EDITOR
    [PublicAPI] public static ISerializedRW<A> unityObjectSerializedRW<A>() where A : Object =>
      PathStr.serializedRW.map(
        path => {
          try {
            return Either<string, A>.Right(UnityEditor.AssetDatabase.LoadAssetAtPath<A>(path));
          }
          catch (Exception e) {
            return $"loading {typeof(A).FullName} from '{path}' threw {e}";
          }
        },
        module => module.editorAssetPath()
      );
#endif

    /// <summary>Serialized RW for a type that has no parameters (like <see cref="Unit"/>)</summary>
    [PublicAPI]
    public static ISerializedRW<A> unitType<A>() where A : new() =>
      Unit.rw.mapNoFail(_ => new A(), _ => F.unit);

    [PublicAPI]
    public static ISerializedRW<A> a<A>(
      ISerializer<A> serializer, IDeserializer<A> deserializer
    ) => new JointRW<A>(serializer, deserializer);

    [PublicAPI]
    public static ISerializer<B> mapSerialize<A, B>(
      this ISerializer<A> a, Func<B, A> mapper
    ) => new MappedSerializer<A, B>(a, mapper);

    [PublicAPI]
    public static IDeserializer<B> map<A, B>(
      this IDeserializer<A> a, Func<A, Either<string, B>> mapper
    ) => new MappedDeserializer<A, B>(a, mapper);

    [PublicAPI]
    public static ISerializedRW<B> mapNoFail<A, B>(
      this ISerializedRW<A> aRW,
      Func<A, B> deserializeConversion,
      Func<B, A> serializeConversion
    ) => aRW.map(
      a => Either<string, B>.Right(deserializeConversion(a)), 
      serializeConversion
    );

    public static ISerializedRW<B> map<A, B>(
      this ISerializedRW<A> aRW,
      Func<A, Either<string, B>> deserializeConversion,
      Func<B, A> serializeConversion
    ) => new MappedRW<A, B>(aRW, serializeConversion, deserializeConversion);
    
    public static ISerializedRW<A> mapError<A>(
      this ISerializedRW<A> aRW,
      Func<string, string> mapper
    ) => new Lambda<A>(
      aRW.serialize,
      (serialized, index) => {
        var either = aRW.deserialize(serialized, index);
        return either.leftValueOut(out var err) ? mapper(err) : either;
      }
    );

    public static ISerializedRW<B> mapTry<A, B>(
      this ISerializedRW<A> aRW,
      Func<A, B> deserializeConversion,
      Func<B, A> serializeConversion
    ) => new MappedRW<A, B>(aRW, serializeConversion, a => {
      try {
        return deserializeConversion(a);
      }
      catch (Exception e) {
        return $"mapping from {typeof(A).FullName} to {typeof(B).FullName} threw {e}";
      }
    });

    public static ISerializedRW<A> lambda<A>(
      Serialize<A> serialize, Deserialize<DeserializeInfo<A>> deserialize
    ) => new Lambda<A>(serialize, deserialize);

    // ReSharper disable TypeParameterCanBeVariant
    public delegate Rope<byte> SerializeAggregate<A, Meta>(A a, Meta meta);
    public delegate Either<string, DeserializeInfo<A>> DeserializeAggregate<A, Meta>(
      byte[] data, int offset, Meta meta, A current
    );
    // ReSharper restore TypeParameterCanBeVariant
    /// <summary>
    /// Serialize value with multiple serializers aggregated together.
    /// </summary>
    /// <param name="metas"></param>
    /// <param name="startingValue"></param>
    /// <param name="serialize"></param>
    /// <param name="deserialize"></param>
    public static ISerializedRW<A> aggregate<A, Meta>(
      IEnumerable<Meta> metas, A startingValue, 
      SerializeAggregate<A, Meta> serialize,
      DeserializeAggregate<A, Meta> deserialize
    ) => lambda(
      a => metas.Aggregate(
        Rope.a(F.emptyArray<byte>()),
        (current, meta) => current + serialize(a, meta)
      ),
      (bytes, offset) => metas.Aggregate(
        Either<string, DeserializeInfo<A>>.Right(
          new DeserializeInfo<A>(startingValue, 0)
        ),
        (currentEither, meta) => {
          if (currentEither.rightValueOut(out var currentInfo)) {
            var newEither = deserialize(bytes, offset + currentInfo.bytesRead, meta, currentInfo.value);
            return 
              newEither.rightValueOut(out var readInfo) 
                ? new DeserializeInfo<A>(readInfo.value, currentInfo.bytesRead + readInfo.bytesRead) 
                : newEither;
          }
          else {
            return currentEither;
          }
        }
      )
    );

    public static ISerializedRW<OneOf<A, B, C>> oneOf<A, B, C>(
      ISerializedRW<A> aRW, ISerializedRW<B> bRW, ISerializedRW<C> cRW
    ) => new OneOfRW<A, B, C>(aRW, bRW, cRW);

    public static ISerializedRW<KeyValuePair<A, B>> kv<A, B>(
      this ISerializedRW<A> aRW, ISerializedRW<B> bRW
    ) => and(aRW, bRW, (a, b) => new KeyValuePair<A, B>(a, b), t => t.Key, t => t.Value);

    public static ISerializedRW<Tpl<A, B>> tpl<A, B>(
      this ISerializedRW<A> aRW, ISerializedRW<B> bRW
    ) => and(aRW, bRW, F.t, t => t._1, t => t._2);

    public static ISerializedRW<Tpl<A, B, C>> tpl<A, B, C>(
      this ISerializedRW<A> aRW, ISerializedRW<B> bRW, ISerializedRW<C> cRW
    ) => and(aRW, bRW, cRW, F.t, t => t._1, t => t._2, t => t._3);

    public static ISerializedRW<B> and<A1, A2, B>(
      this ISerializedRW<A1> a1RW, ISerializedRW<A2> a2RW,
      Func<A1, A2, B> mapper, Func<B, A1> getA1, Func<B, A2> getA2
    ) => new AndRW2<A1, A2, B>(a1RW, a2RW, mapper, getA1, getA2);

    public static ISerializedRW<B> and<A1, A2, A3, B>(
      this ISerializedRW<A1> a1RW, ISerializedRW<A2> a2RW, ISerializedRW<A3> a3RW,
      Func<A1, A2, A3, B> mapper, Func<B, A1> getA1, Func<B, A2> getA2, Func<B, A3> getA3
    ) => new AndRW3<A1, A2, A3, B>(a1RW, a2RW, a3RW, mapper, getA1, getA2, getA3);

    public static ISerializedRW<B> and<A1, A2, A3, A4, B>(
      this ISerializedRW<A1> a1RW, ISerializedRW<A2> a2RW, ISerializedRW<A3> a3RW,
      ISerializedRW<A4> a4RW,
      Func<A1, A2, A3, A4, B> mapper, Func<B, A1> getA1, Func<B, A2> getA2, Func<B, A3> getA3, Func<B, A4> getA4
    ) => new AndRW4<A1, A2, A3, A4, B>(a1RW, a2RW, a3RW, a4RW, mapper, getA1, getA2, getA3, getA4);

    public static ISerializedRW<B> and<A1, A2, A3, A4, A5, B>(
      this ISerializedRW<A1> a1RW, ISerializedRW<A2> a2RW, ISerializedRW<A3> a3RW,
      ISerializedRW<A4> a4RW, ISerializedRW<A5> a5RW,
      Func<A1, A2, A3, A4, A5, B> mapper, Func<B, A1> getA1, Func<B, A2> getA2, Func<B, A3> getA3, 
      Func<B, A4> getA4, Func<B, A5> getA5
    ) => new AndRW5<A1, A2, A3, A4, A5, B>(
      a1RW, a2RW, a3RW, a4RW, a5RW, mapper, getA1, getA2, getA3, getA4, getA5
    );

    public static ISerializedRW<B> and<A1, A2, A3, A4, A5, A6, B>(
      this ISerializedRW<A1> a1RW, ISerializedRW<A2> a2RW, ISerializedRW<A3> a3RW,
      ISerializedRW<A4> a4RW, ISerializedRW<A5> a5RW, ISerializedRW<A6> a6RW,
      Func<A1, A2, A3, A4, A5, A6, B> mapper, Func<B, A1> getA1, Func<B, A2> getA2, Func<B, A3> getA3, 
      Func<B, A4> getA4, Func<B, A5> getA5, Func<B, A6> getA6
    ) => new AndRW6<A1, A2, A3, A4, A5, A6, B>(
      a1RW, a2RW, a3RW, a4RW, a5RW, a6RW, mapper, getA1, getA2, getA3, getA4, getA5, getA6
    );

    public static ISerializedRW<B> and<A1, A2, A3, A4, A5, A6, A7, B>(
      this ISerializedRW<A1> a1RW, ISerializedRW<A2> a2RW, ISerializedRW<A3> a3RW,
      ISerializedRW<A4> a4RW, ISerializedRW<A5> a5RW, ISerializedRW<A6> a6RW, ISerializedRW<A7> a7RW,
      Func<A1, A2, A3, A4, A5, A6, A7, B> mapper, Func<B, A1> getA1, Func<B, A2> getA2, Func<B, A3> getA3, 
      Func<B, A4> getA4, Func<B, A5> getA5, Func<B, A6> getA6, Func<B, A7> getA7
    ) => new AndRW7<A1, A2, A3, A4, A5, A6, A7, B>(
      a1RW, a2RW, a3RW, a4RW, a5RW, a6RW, a7RW, mapper, getA1, getA2, getA3, getA4, getA5, getA6, getA7
    );

    [PublicAPI]
    public static ISerializedRW<Option<A>> opt<A>(ISerializedRW<A> rw) =>
      new OptRW<A>(rw);

    [PublicAPI]
    public static ISerializedRW<Either<A, B>> either<A, B>(ISerializedRW<A> aRW, ISerializedRW<B> bRW) =>
      new EitherRW<A, B>(aRW, bRW);

    [PublicAPI]
    public static ISerializedRW<ImmutableArray<A>> immutableArray<A>(
      ISerializedRW<A> rw
    ) => a(
      collectionSerializer<A, ImmutableArray<A>>(rw), 
      collectionDeserializer(rw, CollectionBuilderKnownSizeFactory<A>.immutableArray)
    );

    [PublicAPI]
    public static ISerializedRW<ImmutableList<A>> immutableList<A>(
      ISerializedRW<A> rw
    ) => a(
      collectionSerializer<A, ImmutableList<A>>(rw), 
      collectionDeserializer(rw, CollectionBuilderKnownSizeFactory<A>.immutableList)
    );

    [PublicAPI]
    public static ISerializedRW<ImmutableHashSet<A>> immutableHashSet<A>(
      ISerializedRW<A> rw
    ) => a(
      collectionSerializer<A, ImmutableHashSet<A>>(rw), 
      collectionDeserializer(rw, CollectionBuilderKnownSizeFactory<A>.immutableHashSet)
    );

    [PublicAPI]
    public static ISerializedRW<ImmutableDictionary<K, V>> immutableDictionary<K, V>(
      ISerializedRW<K> kRw, ISerializedRW<V> vRw
    ) => immutableDictionary(kv(kRw, vRw));

    [PublicAPI]
    public static ISerializedRW<ImmutableDictionary<K, V>> immutableDictionary<K, V>(
      ISerializedRW<KeyValuePair<K, V>> rw
    ) => a(
      collectionSerializer<KeyValuePair<K, V>, ImmutableDictionary<K, V>>(rw),
      collectionDeserializer(rw, CollectionBuilderKnownSizeFactoryKV<K, V>.immutableDictionary)
    );

    [PublicAPI]
    public static ISerializedRW<Dictionary<K, V>> dictionary<K, V>(
      ISerializedRW<K> kRw, ISerializedRW<V> vRw
    ) => dictionary(kv(kRw, vRw));

    [PublicAPI]
    public static ISerializedRW<Dictionary<K, V>> dictionary<K, V>(
      ISerializedRW<KeyValuePair<K, V>> rw
    ) => a(
      collectionSerializer<KeyValuePair<K, V>, Dictionary<K, V>>(rw),
      collectionDeserializer(rw, CollectionBuilderKnownSizeFactoryKV<K, V>.dictionary)
    );

    [PublicAPI]
    public static ISerializedRW<A[]> array<A>(
      ISerializedRW<A> rw
    ) => a(
      collectionSerializer<A, A[]>(rw),
      collectionDeserializer(rw, CollectionBuilderKnownSizeFactory<A>.array)
    );

    [PublicAPI]
    public static ISerializer<ICollection<A>> collectionSerializer<A>(ISerializer<A> serializer) =>
      collectionSerializer<A, ICollection<A>>(serializer);

    [PublicAPI]
    public static ISerializer<C> collectionSerializer<A, C>(
      ISerializer<A> serializer
    ) where C : ICollection<A> =>
      new ICollectionSerializer<A, C>(serializer);

    [PublicAPI]
    public static IDeserializer<C> collectionDeserializer<A, C>(
      IDeserializer<A> deserializer, CollectionBuilderKnownSizeFactory<A, C> factory
    ) => new CollectionDeserializer<A, C>(deserializer, factory);
  }
}