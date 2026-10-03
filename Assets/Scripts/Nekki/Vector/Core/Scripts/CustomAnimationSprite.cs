using System.Collections.Generic;
using UnityEngine;

namespace Nekki.Vector.Core.Scripts
{
	public class CustomAnimationSprite : AnimationSprite
	{
		private List<KeyValuePair<Sprite, int>> _Frames;

		private SpriteRenderer _SpriteRender;

		private int _CurrentFrame;

		private float _Times = 1f;

		private float _FPS = 10f;

		public override void Init(string p_name, SpriteRenderer p_spriteRender)
		{
			_Frames = ResourcesMap.GetCustomFramesSequence(p_name);
			_TotalFrames = TotalFrames();
			if (_TotalFrames == 0)
			{
				DebugUtils.Dialog(string.Format("Error create animation by name={0}", p_name), false);
				return;
			}
			_SpriteRender = p_spriteRender;
			_SpriteRender.sprite = _Frames[0].Key;
		}

		// A quest can swap the artwork on this same visual. Keep its authored
		// width/height even when the next GIF has different pixel dimensions.
		public bool TryChangeSequence(string manifest)
		{
			if (_SpriteRender == null || string.IsNullOrEmpty(manifest)) return false;
			List<KeyValuePair<Sprite, int>> next;
			try { next = ResourcesMap.GetCustomFramesSequence(manifest); }
			catch { return false; }
			if (next == null || next.Count == 0 || next[0].Key == null) return false;
			Sprite previous = _SpriteRender.sprite;
			if (previous != null)
			{
				Vector3 scale = transform.localScale;
				scale.x *= previous.rect.width / next[0].Key.rect.width;
				scale.y *= previous.rect.height / next[0].Key.rect.height;
				transform.localScale = scale;
			}
			_Frames = next;
			_TotalFrames = TotalFrames();
			_CurrentFrame = 0;
			_Times = 1f;
			_SpriteRender.sprite = next[0].Key;
			IsWork = true;
			return true;
		}

		public override void SetSpriteFrame(int p_index)
		{
			if (p_index < _TotalFrames)
			{
				int i;
				for (i = 0; p_index > _Frames[i].Value; i++)
				{
					p_index -= _Frames[i].Value;
				}
				_SpriteRender.sprite = _Frames[i].Key;
			}
		}

		public override void AdvanceSimulationTick()
		{
			if (base.IsWork && GameTiming.GameplayVisualsCanAdvance)
			{
				if (_Times >= 1f / _FPS)
				{
					SetSpriteAnimation();
					_Times = 0f;
				}
				else
				{
					_Times += GameTiming.SimulationStep;
				}
			}
		}

		private int TotalFrames()
		{
			int num = 0;
			foreach (KeyValuePair<Sprite, int> frame in _Frames)
			{
				num += frame.Value;
			}
			return num;
		}
	}
}
