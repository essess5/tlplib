using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using com.tinylabproductions.TLPLib.Functional;

using Smooth.Slinq;

#if !UNITY_3_5
namespace Smooth.Slinq.Test {
#endif
	
	public class OrderByLinq : MonoBehaviour {
		private void Update() {
			for (int i = 0; i < SlinqTest.loops; ++i) {
				SlinqTest.Tpls1.OrderBy(SlinqTest.to_1f).FirstOrDefault();
			}
		}
	}

#if !UNITY_3_5
}
#endif
