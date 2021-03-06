﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Data;
using Assets.Scripts.Interfaces;
using Assets.Scripts.Managers;
using Assets.Scripts.StateMachine;
using Assets.Scripts.StateMachine.States;
using UnityEngine;

namespace Assets.Scripts.Unit
{
    public class Unit : MonoBehaviour, ISelectable
    {
        public static HashSet<Unit> AllUnits = new HashSet<Unit>();

        [SerializeField] private SpriteRenderer spriteRenderer;
        private Animator animator;

        public UnitData unitData;
        public StateMachine.StateMachine stateMachine;

        public ISelectable Target { get; set; }

        public Vector3 MoveDestination { get; private set; }

        public bool Selected { get; set; }
        public int Team { get; set; }

        private List<State> States;
	
		public delegate void OnUnitDestroy(Unit unit);
		public OnUnitDestroy OnUnitDestroyed;

        private void Awake()
        {
            States = GetComponents<State>().ToList();
            animator = GetComponent<Animator>();
            Team = unitData.team;
        }

        private void Start()
        {
            AllUnits.Add(this);

            if (SelectionManager.Instance.m_Team == Team)
                PopulationManager.Instance.CurrentPopulation++;
        }

        public void Select(int? team)
        {
            Selected = true;
            spriteRenderer.enabled = true;

            if (SelectionManager.SelectedUnits.Contains(this))
                return;

            SelectionManager.SelectedUnits.Add(this);

            if (team == Team)
                SelectionManager.OnTarget += OnTarget;

            if (!SelectionManager.SelectedEntities.Contains(this))
                SelectionManager.SelectedEntities.Add(this);
        }

        public void Deselect()
        {
            Selected = false;
            spriteRenderer.enabled = false;

            if (!SelectionManager.SelectedUnits.Contains(this)) return;

            SelectionManager.SelectedUnits.Remove(this);
            SelectionManager.OnTarget -= OnTarget;

            if (SelectionManager.SelectedEntities.Contains(this))
                SelectionManager.SelectedEntities.Remove(this);
        }

        public IEnumerator Highlight()
        {
            for (int i = 0; i < 6; i++)
            {
				if(spriteRenderer!=null)
					spriteRenderer.enabled = !spriteRenderer.enabled;

                yield return new WaitForSeconds(.2f);
            }

            if (!Selected && spriteRenderer!=null) 
				spriteRenderer.enabled = false;
        }

        public Type GetAction(Unit unit, int? team)
        {
            if (team == Team)
            {
                unit.Target = null;
                unit.MoveDestination = transform.position;

                return typeof(MoveState);
            }

            unit.Target = this;
            return typeof(AttackState);
        }

        public Type GetActionWithoutTarget(Unit unit, int? team)
        {
            return team == Team ? typeof(MoveState) : typeof(AttackState);
        }

        public void GetTargetAction()
        {
            if (Target.Equals(null)) { ChangeState(typeof(IdleState)); return; }

            ChangeState(Target.GetAction(this, Team));
        }

        public void OnTarget(ISelectable _target)
        {
            if (_target == null) return;

            ChangeState(_target.GetAction(this, Team));
        }

        public void FindAnotherTarget()
        {
            var collides = Physics.OverlapSphere(transform.position, unitData.SearchRange);
            ISelectable nextTarget = null;
            float distance = 999f;

            foreach (var collider in collides)
            {
                var tempTarget = collider.GetComponent<ISelectable>();

                if (tempTarget == null || tempTarget.GetActionWithoutTarget(this, Team) != stateMachine.currentState.GetType())
                    continue;

                if (tempTarget.transform.position.DistanceXZ(transform.position) < distance)
                {
                    nextTarget = tempTarget;
                    distance = nextTarget.transform.position.DistanceXZ(transform.position);
                }
            }

            if (nextTarget != null)
            {
                OnTarget(nextTarget);
                return;
            }

            ChangeState(typeof(IdleState));
            Target = null;
        }

        public void MoveToPosition(Vector3 position)
        {
            MoveDestination = position;
            Target = null;

            ChangeState(typeof(MoveState));
        }

        public bool HasState(Type type)
        {
            return States.Any(state => state.GetType() == type);
        }

        public void ChangeState(Type type)
        {
            foreach (var state in States.Where(state => state.GetType() == type))
            {
                stateMachine.ChangeState(state);

                return;
            }

            Debug.LogWarning("This unit can't perform " + type);

            Target = null;
            ChangeState(typeof(IdleState));
        }

        public void ChangeState(State state)
        {
            ChangeState(state.GetType());
        }

        private void OnDestroy()
        {
			OnUnitDestroyed?.Invoke(this);

            AllUnits.Remove(this);

            if (SelectionManager.Instance != null)
            {
                Deselect();
                PopulationManager.Instance.CurrentPopulation--;
            }
        }
    }
}