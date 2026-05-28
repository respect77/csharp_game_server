namespace Server.Common
{
    public abstract class StateBase<TState> where TState : struct, IConvertible
    {
        private TState _state;

        protected StateBase(TState state)
        {
            _state = state;
        }

        public TState GetState() => _state;

        public abstract bool Update();

        public virtual bool Enter()
        {
            return false;
        }

        public virtual void Exit()
        {
        }
    }

    public class StateMachine<TState> where TState : struct, IConvertible
    {
        private StateBase<TState> _currentState;
        private readonly Dictionary<TState, StateBase<TState>> _states = new();
        private readonly Dictionary<TState, TState> _nextState = new();

        public StateMachine(List<StateBase<TState>> stateList)
        {
            if (stateList.Count == 0)
            {
                throw new ArgumentException("stateList must contain at least one state");
            }

            TState? beforeStateType = null;
            foreach (var state in stateList)
            {
                var stateType = state.GetState();
                if (_states.ContainsKey(stateType))
                {
                    throw new Exception($"_stateBases.ContainsKey(stateType): {stateType}");
                }

                _states[stateType] = state;
                if (beforeStateType != null)
                {
                    _nextState[beforeStateType.Value] = stateType;
                }
                beforeStateType = state.GetState();
            }
            _currentState = stateList[0];
            _currentState.Enter();
            _nextState[stateList[^1].GetState()] = _currentState.GetState();
        }

        public TState GetCurrentState()
        {
            return _currentState.GetState();
        }

        public void ChangeState(TState stateType)
        {
            if (!_states.TryGetValue(stateType, out StateBase<TState>? nextState))
            {
                Console.WriteLine($"StateMachine: ChangeState: stateType:{stateType} Not Found!");
                return;
            }

            _currentState.Exit();
            _currentState = nextState;
            if (_currentState.Enter())
            {
                ChangeNextState();
            }
        }

        public void Update()
        {
            if (!_currentState.Update())
                return;

            ChangeNextState();
        }

        private void ChangeNextState()
        {
            if (_nextState.TryGetValue(_currentState.GetState(), out var nextState))
            {
                ChangeState(nextState);
            }
        }
    }
}
