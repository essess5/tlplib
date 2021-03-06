﻿using com.tinylabproductions.TLPLib.Components.Interfaces;
using com.tinylabproductions.TLPLib.Functional;

namespace com.tinylabproductions.TLPLib.Components.Forwarders {
  public class OnMouseUpForwarder : EventForwarder<Unit>, IMB_OnMouseUp {
    public void OnMouseUp() => _onEvent.push(F.unit);
  }
}
