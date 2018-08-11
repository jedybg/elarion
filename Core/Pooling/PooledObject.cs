﻿using UnityEngine;
using UnityEngine.Events;

namespace Elarion.Pooling {
    public class PooledObject : MonoBehaviour {

        public UnityEvent onInitialize = new UnityEvent();
        public UnityEvent onDeinitialize = new UnityEvent();

        [SerializeField, HideInInspector]
        private ObjectPool _pool;

        internal ObjectPool Pool {
            get => _pool;
            set => _pool = value;
        }

        public void Awake() {
            gameObject.SetActive(false);
        }
        
        public void Initialize() {
            OnInitialize();
            onInitialize.Invoke();
            gameObject.SetActive(true);
        }

        public void ReturnToPool() {
            Pool.Return(this);
        }

        internal void Deinitialize() {
            OnDeinitialize();
            gameObject.SetActive(false);
            onDeinitialize.Invoke();
        }

        protected virtual void OnInitialize() { }

        protected virtual void OnDeinitialize() { }
    }
}