using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Elarion.Attributes;
using Elarion.Extensions;
using Elarion.UI.Helpers;
using Elarion.UI.Helpers.Animation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;

namespace Elarion.UI {
    // TODO simple loading helper - sets loading to true/false based on delegate/unity event
    // TODO simple hoverable/pressable helpers - set hovered/pressed based on unity events

    // TODO Make an unfocusable UIElement (use it to animate inputs and similar shit)

    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public abstract class UIComponent : UIState {
        [SerializeField]
        private UIOpenType _openType = UIOpenType.OpenWithParent;

        private UIRoot _uiRoot;

        private UIAnimator _animator;
        private IAnimationController[] _animationControllers = { };
        private List<UIComponent> _childComponents = new List<UIComponent>();

        public UIComponent ParentComponent { get; private set; }

        protected List<UIComponent> ChildComponents {
            get { return _childComponents; }
            set { _childComponents = value; }
        }

        public UIAnimator Animator {
            get {
                if(_animator == null) {
                    _animator = GetComponent<UIAnimator>();
                }

                return _animator;
            }
        }

        public UIOpenConditions OpenConditions { get; protected set; }

        public UIOpenType OpenType {
            get { return _openType; }
        }

        public virtual bool IsRendering {
            get { return IsOpened || IsInTransition || IsRenderingChild; }
        }

        public virtual bool IsRenderingChild {
            get { return ChildComponents.Any(child => child.IsRendering); }
        }

        protected virtual bool InteractableSelf {
            get { return true; }
            // ReSharper disable once ValueParameterNotUsed
            set { }
        }

        protected bool InteractableParent {
            get { return ParentComponent == null || ParentComponent.IsInteractable; }
        }

        public bool HasAnimator {
            get { return Animator != null && Animator.enabled; }
        }

        public bool IsAnimating {
            get {
                foreach(var animationController in _animationControllers) {
                    if(animationController.Animating) {
                        return true;
                    }
                }

                return false;
            }
        }

        public abstract float Alpha { get; set; }

        public abstract Behaviour Renderer { get; }

        protected virtual bool CanOpen {
            get {
                if(ParentComponent && (!ParentComponent.isActiveAndEnabled || !ParentComponent.IsOpened)) {
                    return false;
                }

                if(IsOpened || !isActiveAndEnabled) {
                    return false;
                }

                if(OpenConditions && !OpenConditions.CanOpen) {
                    return false;
                }

                return true;
            }
        }
        
        public void Open(bool skipAnimation = false) {
            var animation = skipAnimation ? null : GetAnimation(UIAnimationType.OnOpen);

            OpenInternal(animation, true);
        }

        public void Open(UIAnimation overrideAnimation) {
            var animation = overrideAnimation != null ? overrideAnimation : GetAnimation(UIAnimationType.OnOpen);
            
            OpenInternal(animation, true);
        }
        
        public void Close(bool skipAnimation = false) {
            var animation = skipAnimation ? null : GetAnimation(UIAnimationType.OnClose);
            CloseInternal(animation, true);
        }

        public void Close(UIAnimation overrideAnimation) {
            var animation = overrideAnimation != null ? overrideAnimation : GetAnimation(UIAnimationType.OnClose);
            
            CloseInternal(animation, true);
        }

        protected override void Awake() {
            base.Awake();
            _animator = GetComponent<UIAnimator>();
            _animationControllers = GetComponents<IAnimationController>();

            OpenConditions = GetComponent<UIOpenConditions>();
        }

        protected override void OnEnable() {
            base.OnEnable();

            UpdateParent();

            ChildComponents = GetComponentsInChildren<UIComponent>(includeInactive: true)
                .Where(child => child.ParentComponent == this).ToList();

            OnStateChanged(currentState, previousState);
        }

        protected override void OnDisable() {
            base.OnDisable();

            if(!IsOpened) return;

            Close(true);

            OnStateChanged(currentState, previousState);
        }

        /// <summary>
        /// Updates the topmost UIComponents. UIComponents then propagate the state update to their children.
        /// </summary>
        protected override void Update() {
            if(ParentComponent) {
                return;
            }

            // TODO register all top-level components in the UIRoot (UIManager); Update them from there

            UpdateComponent();
        }

        protected virtual void BeforeOpen(bool skipAnimation) { }

        protected virtual void OpenInternal(UIAnimation animation, bool isEventOrigin) {
            if(isEventOrigin && !gameObject.activeSelf) {
                gameObject.SetActive(true);
            }

            if(!CanOpen) {
                return;
            }

            var noAnimation = animation == null;

            BeforeOpen(noAnimation);

            IsOpened = true;

            OpenChildren(UIOpenType.OpenWithParent, noAnimation);

            if(noAnimation) {
                OpenChildren(UIOpenType.OpenAfterParent, true);
            }

            if(!HasAnimator || noAnimation) {
                AfterOpen(isEventOrigin);
                return;
            }

            Animator.ResetToSavedProperties();

            Animator.Play(animation, callback: () => AfterOpen(isEventOrigin));
        }

        /// <summary>
        /// Called after the object has been opened and all open animations have finished playing (if any)
        /// </summary>
        /// <param name="isEventOrigin"></param>
        protected virtual void AfterOpen(bool isEventOrigin) {
            OpenChildren(UIOpenType.OpenAfterParent, false);

            // send another select event to the selected component; otherwise Unity is likely not to focus it 
            if(UIRoot && UIRoot.SelectedObject && UIRoot.SelectedObject.transform.IsChildOf(transform)) {
                UIRoot.Select(UIRoot.SelectedObject);
            }
        }

        protected void OpenChildren(UIOpenType openTypeFilter, bool skipAnimation) {
            foreach(var child in ChildComponents) {
                if(!child.CanOpen ||
                   child.OpenType != openTypeFilter) {
                    continue;
                }
                
                var childAnimation = skipAnimation ? null : child.GetAnimation(UIAnimationType.OnOpen);

                child.OpenInternal(childAnimation, false);
            }
        }

        protected virtual void BeforeClose() { }

        /// <summary>
        /// Close implementation. Override this to modify the base functionality.
        /// </summary>
        /// <param name="animation">The aimation to play while closing (can be null).</param>
        /// <param name="isEventOrigin">The initial closed element has isEventOrigin set to true.
        /// Its' child objects (that also get closed) have isEventOrigin set to false.</param>
        protected virtual void CloseInternal(UIAnimation animation, bool isEventOrigin) {
            if(!IsOpened) {
                return;
            }

            BeforeClose();

            IsOpened = false;

            var noAnimation = animation == null;

            foreach(var child in ChildComponents) {
                var childAnimation = noAnimation ? null : child.GetAnimation(UIAnimationType.OnClose);
                
                child.CloseInternal(childAnimation, false);
            }

            if(!HasAnimator || noAnimation) {
                AfterClose();
                return;
            }

            Animator.Play(animation, callback: AfterClose);
        }

        /// <summary>
        /// Called after the object has been closed and all close animations have finished playing (if any)
        /// </summary>
        protected void AfterClose() {
            if(HasAnimator) {
                Animator.ResetToSavedProperties();
                Renderer.enabled = false; // instantly hide this, the state will update on the next frame
            }
        }

        protected virtual void UpdateComponent() {
            foreach(var childComponent in ChildComponents) {
                if(!childComponent) {
                    Debug.LogWarning("Trying to update a child component that's missing.", gameObject);
                }

                childComponent.UpdateComponent();
            }

            UpdateState();
        }

        protected override void UpdateState() {
            IsInteractable = IsOpened && !IsDisabled && !IsInTransition && InteractableSelf &&
                             InteractableParent;

            // Set the state to InTransition if either this is animating or this can't animate and its' parent is animating
            IsInTransition = IsAnimating ||
                             !HasAnimator && ParentComponent != null && ParentComponent.IsInTransition;

            if(IsStateDirty) {
                // TODO fire any events here; Fire the blur event in the UIPanel
            }

            // Finish updating the state
            base.UpdateState();
        }

        protected UIAnimation GetAnimation(UIAnimationType type) {
            return !HasAnimator ? null : Animator.GetAnimation(type);

        }

        protected override void OnStateChanged(States currentState, States previousState) {
            Renderer.enabled = IsRendering;

            base.OnStateChanged(currentState, previousState);
        }

        private void UpdateParent() {
            if(Transform.parent != null) {
                ParentComponent = Transform.parent.GetComponentsInParent<UIComponent>(includeInactive: true)
                    .FirstOrDefault();
                return;
            }

            ParentComponent = null;
        }

        protected override void OnTransformParentChanged() {
            base.OnTransformParentChanged();
            UpdateParent();
        }

        protected override void OnValidate() {
            base.OnValidate();

            UpdateParent();

            // top level elements
            if(ParentComponent == null) {
                _openType = UIOpenType.OpenManually;
            }
        }

        public string Description {
            get {
                StringBuilder stringBuilder = new StringBuilder();
                stringBuilder.AppendLine("<b>" + GetType().Name + ": </b>" + name);
                stringBuilder.AppendLine("<b>Opened: </b>" + IsOpened);
                stringBuilder.AppendLine("<b>Rendering: </b>" + IsRendering);
                stringBuilder.AppendLine("<b>In Transition: </b>" + IsInTransition);
                stringBuilder.AppendLine("<b>Focused: </b>" + IsFocusedThis);
                stringBuilder.AppendLine("<b>Disabled: </b>" + IsDisabled);
                stringBuilder.AppendLine("<b>Interactable: </b>" + IsInteractable);
                stringBuilder.AppendLine("<b>Visible Child: </b>" + IsRenderingChild);
                return stringBuilder.ToString();
            }
        }

        // TODO get rid of this
        protected UIRoot UIRoot {
            get {
                if(_uiRoot == null) {
                    _uiRoot = UIRoot.UIRootCache.SingleOrDefault(root => root.transform.IsParentOf(transform));

                    if(_uiRoot == null) {
                        _uiRoot = GetComponentInParent<UIRoot>();
                    }
                }

                return _uiRoot;
            }
        }
    }
}