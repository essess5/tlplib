﻿using System;
using com.tinylabproductions.TLPLib.Collection;
using com.tinylabproductions.TLPLib.Functional;

namespace com.tinylabproductions.TLPLib.Data.serialization {
  class MappedSerializer<A, B> : ISerializer<B> {
    readonly ISerializer<A> aSerializer;
    readonly Func<B, A> mapper;

    public MappedSerializer(ISerializer<A> aSerializer, Func<B, A> mapper) {
      this.aSerializer = aSerializer;
      this.mapper = mapper;
    }

    public static Rope<byte> serialize(ISerializer<A> aSer, Func<B, A> mapper, B b) =>
      aSer.serialize(mapper(b));

    public Rope<byte> serialize(B b) => serialize(aSerializer, mapper, b);
  }

  class MappedDeserializer<A, B> : IDeserializer<B> {
    readonly IDeserializer<A> aDeserializer;
    readonly Func<A, Option<B>> mapper;

    public MappedDeserializer(IDeserializer<A> aDeserializer, Func<A, Option<B>> mapper) {
      this.aDeserializer = aDeserializer;
      this.mapper = mapper;
    }

    public Option<DeserializeInfo<B>> deserialize(byte[] serialized, int startIndex) =>
      deserialize(aDeserializer, mapper, serialized, startIndex);

    public static Option<DeserializeInfo<B>> deserialize(
      IDeserializer<A> aDeserializer, Func<A, Option<B>> mapper,
      byte[] serialized, int startIndex
    ) {
      var aInfoOpt = aDeserializer.deserialize(serialized, startIndex);
      if (aInfoOpt.isNone) return Option<DeserializeInfo<B>>.None;
      var aInfo = aInfoOpt.get;
      var bOpt = mapper(aInfo.value);
      if (bOpt.isNone) return Option<DeserializeInfo<B>>.None;
      var bInfo = new DeserializeInfo<B>(bOpt.get, aInfo.bytesRead);
      return F.some(bInfo);
    }
  }

  class MappedRW<A, B> : ISerializedRW<B> {
    readonly ISerializedRW<A> aRW;
    readonly Func<B, A> serializeConversion;
    readonly Func<A, Option<B>> deserializeConversion;

    public MappedRW(
      ISerializedRW<A> aRw, Func<B, A> serializeConversion,
      Func<A, Option<B>> deserializeConversion
    ) {
      aRW = aRw;
      this.serializeConversion = serializeConversion;
      this.deserializeConversion = deserializeConversion;
    }

    public Option<DeserializeInfo<B>> deserialize(byte[] serialized, int startIndex) =>
      MappedDeserializer<A, B>.deserialize(aRW, deserializeConversion, serialized, startIndex);

    public Rope<byte> serialize(B b) =>
      MappedSerializer<A, B>.serialize(aRW, serializeConversion, b);
  }
}