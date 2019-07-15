﻿
using System.Linq;
using com.tinylabproductions.TLPLib.Components.Interfaces;
using com.tinylabproductions.TLPLib.Extensions;
using com.tinylabproductions.TLPLib.Functional;
using JetBrains.Annotations;
using Sirenix.OdinInspector;
using UnityEngine;

namespace com.tinylabproductions.TLPLib.Components.sorting_layer {
  // [Help(
  //   HelpType.Info, HelpPosition.Before,
  //   "Sets the sorting layer on this game object to the one specified in" +
  //   "this component."
  // )]
  public abstract partial class SortingLayerSetter : MonoBehaviour, IMB_Awake {
    public struct SortingLayerAndOrder {
      public readonly int layerId, order;

      public SortingLayerAndOrder(int layerId, int order) {
        this.layerId = layerId;
        this.order = order;
      }
    }

#pragma warning disable 649
    // ReSharper disable NotNullMemberIsNotInitialized
    [SerializeField, NotNull] SortingLayerReference sortingLayer;
    // ReSharper restore NotNullMemberIsNotInitialized
#pragma warning restore 649

    [ShowInInspector, ValueDropdown(nameof(all))] SortingLayerReference selector {
      get => sortingLayer;
      set => sortingLayer = value;
    }
    
    ValueDropdownList<SortingLayerReference> all {
      get {
        var list = new ValueDropdownList<SortingLayerReference>();
        foreach (var item in Resources
          .FindObjectsOfTypeAll<SortingLayerReference>()
          .OrderBy(_ => _.sortingLayer)
          .ThenBy(_ => _.orderInLayer)
          .Select(_ =>
            new ValueDropdownItem<SortingLayerReference>($"{_.orderInLayer,4}: {_.name}", _))
        ) {
          list.Add(item);
        }
        return list;
      }
    }


    Option<SortingLayerReference> sortingLayerOverride = F.none_;
    public void setSortingLayerOverride(SortingLayerReference sortingLayer) {
      sortingLayerOverride = sortingLayer.some();
      apply(sortingLayer);
    }

    [PublicAPI] protected abstract void apply(SortingLayerReference sortingLayer);
    protected abstract SortingLayerAndOrder extract();
    protected abstract void recordEditorChanges();

    public void Awake() {
#if UNITY_EDITOR
      // This is only needed because we have an editor part of this partial class
      // which runs this script in edit mode.
      if (!Application.isPlaying) return;
#endif

      apply(sortingLayerOverride.getOrElse(sortingLayer));
    }
  }
}