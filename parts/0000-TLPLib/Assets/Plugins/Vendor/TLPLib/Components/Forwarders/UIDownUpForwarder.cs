﻿using com.tinylabproductions.TLPLib.Components.Interfaces;
using com.tinylabproductions.TLPLib.Functional;
using com.tinylabproductions.TLPLib.Reactive;
using UnityEngine.EventSystems;

namespace com.tinylabproductions.TLPLib.Components {
  public class UIDownUpForwarder : UIBehaviour, IPointerDownHandler, IPointerUpHandler, IMB_OnEnable, IMB_OnDisable {
    readonly Subject<Unit> _onDown = new Subject<Unit>();
    readonly Subject<Unit> _onUp = new Subject<Unit>();
    public IObservable<Unit> onDown => _onDown;
    public IObservable<Unit> onUp => _onUp;
    public bool isDown { get; private set; }

    bool disabledWithOnDown;

    void down() {
      _onDown.push(new Unit());
      isDown = true;
    }

    void up() {
      _onUp.push(new Unit());
      isDown = false;
    }

    public void OnPointerDown(PointerEventData eventData) {
      if (eventData.button == PointerEventData.InputButton.Left && IsActive())
        down();
    }

    public void OnPointerUp(PointerEventData eventData) {
      if (eventData.button == PointerEventData.InputButton.Left && IsActive())
        up();
    }

    public new void OnEnable() {
      if (disabledWithOnDown)
        down();
    }

    public new void OnDisable() {
      disabledWithOnDown = isDown;
      up();
    }
  }
}
