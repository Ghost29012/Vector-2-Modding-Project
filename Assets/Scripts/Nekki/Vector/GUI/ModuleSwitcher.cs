using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Nekki.Vector.GUI
{
	public class ModuleSwitcher : MonoBehaviour
	{
		[SerializeField]
		private EventSystem _EventSystem;

		[SerializeField]
		private Image _Background;

		[SerializeField]
		private float _FadeTime = 1.5f;

		[SerializeField]
		private float _PauseTime = 1f;

		private Action _SwitchFunc;

		private Action _ActionAfterSwitch;

		private Sequence _Sequence;

		public bool IsSwitching
		{
			get { return _Sequence != null && _Sequence.IsActive(); }
		}

		public void Switch(Action p_switchFunc, bool p_needFadeOut = true)
		{
			Switch(p_switchFunc, null, p_needFadeOut);
		}

		public void Switch(Action p_switchFunc, Action p_onEndSwitchFunc, bool p_needFadeOut = true)
		{
			_SwitchFunc = p_switchFunc;
			_ActionAfterSwitch = p_onEndSwitchFunc;
			Run(_FadeTime, _PauseTime, p_needFadeOut);
		}

		public void Switch(Action p_switchFunc, float p_fadeTime, float p_pauseTime, bool p_needFadeOut = true)
		{
			Switch(p_switchFunc, null, p_fadeTime, p_pauseTime, p_needFadeOut);
		}

		public void Switch(Action p_switchFunc, Action p_onEndSwitchFunc, float p_fadeTime, float p_pauseTime, bool p_needFadeOut = true)
		{
			_SwitchFunc = p_switchFunc;
			_ActionAfterSwitch = p_onEndSwitchFunc;
			Run(p_fadeTime, p_pauseTime, p_needFadeOut);
		}

		private void Run(float p_fadeTime, float p_pauseTime, bool p_needFadeOut)
		{
			if (_Sequence != null && _Sequence.IsActive()) _Sequence.Kill(false);
			ResetAlpha();
			_EventSystem.enabled = false;
			GetComponent<RectTransform>().SetSiblingIndex(10);
			_Sequence = DOTween.Sequence();
			_Sequence.SetLink(gameObject, LinkBehaviour.KillOnDestroy);
			_Sequence.Append(_Background.DOFade(1f, p_fadeTime));
			_Sequence.AppendInterval(p_pauseTime);
			_Sequence.AppendCallback(SwitchModules);
			if (p_needFadeOut)
			{
				_Sequence.Append(_Background.DOFade(0f, p_fadeTime));
			}
			_Sequence.AppendCallback(OnEndSwitchWrap);
			_Sequence.OnKill(Stop);
			_Sequence.OnComplete(Stop);
			_Sequence.Play();
		}

		public void Stop()
		{
			_Sequence = null;
			if (_EventSystem != null) _EventSystem.enabled = true;
		}

		private void OnDestroy()
		{
			if (_Sequence != null && _Sequence.IsActive()) _Sequence.Kill(false);
			_Sequence = null;
		}

		private void ResetAlpha()
		{
			Color color = _Background.color;
			color.a = 0f;
			_Background.color = color;
		}

		private void SwitchModules()
		{
			if (_SwitchFunc != null)
			{
				_SwitchFunc();
			}
		}

		private void OnEndSwitchWrap()
		{
			if (_ActionAfterSwitch != null)
			{
				_ActionAfterSwitch();
				_ActionAfterSwitch = null;
			}
		}
	}
}
