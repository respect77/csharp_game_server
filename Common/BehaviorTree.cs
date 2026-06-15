
namespace Server.Common
{
    public enum TaskStatus
    {
        Success,
        Failure,
        Continue
    }

    public abstract class BehaviorTreeNode
    {
        public string Name { get; }
        public BehaviorTreeNode()
        {
            Name = GetType().Name;
        }
        public abstract TaskStatus Tick();
    }

    // 조건 노드
    public abstract class ConditionBase : BehaviorTreeNode
    {
        protected abstract bool OnUpdate();
        public override TaskStatus Tick() => OnUpdate() ? TaskStatus.Success : TaskStatus.Failure;
    }

    // 액션 노드
    public abstract class ActionBase : BehaviorTreeNode
    {
        protected abstract TaskStatus OnUpdate();
        public override TaskStatus Tick() => OnUpdate();
    }

    //하나라도 실패하면 종료
    class SequenceNode : BehaviorTreeNode
    {
        private List<BehaviorTreeNode> _children = new();
        private int _currentIndex = 0;
        public SequenceNode(params BehaviorTreeNode[] nodes)
        {
            _children.AddRange(nodes);
        }

        public override TaskStatus Tick()
        {
            while (_currentIndex < _children.Count)
            {
                var state = _children[_currentIndex].Tick();

                if (state == TaskStatus.Continue)
                {
                    return TaskStatus.Continue;
                }

                if (state == TaskStatus.Failure)
                {
                    _currentIndex = 0;
                    return TaskStatus.Failure;
                }

                _currentIndex++;
            }

            _currentIndex = 0;
            return TaskStatus.Success;
        }
    }

    //하나라도 성공하면 종료
    class SelectorNode : BehaviorTreeNode
    {
        private List<BehaviorTreeNode> _children = new();
        private int _currentIndex = 0;
        public SelectorNode(params BehaviorTreeNode[] nodes)
        {
            _children.AddRange(nodes);
        }

        public override TaskStatus Tick()
        {
            while (_currentIndex < _children.Count)
            {
                var state = _children[_currentIndex].Tick();

                if (state == TaskStatus.Continue)
                {
                    return TaskStatus.Continue;
                }

                if (state == TaskStatus.Success)
                {
                    _currentIndex = 0;
                    return TaskStatus.Success;
                }

                _currentIndex++;
            }

            _currentIndex = 0;
            return TaskStatus.Failure;
        }
    }

    public abstract class DecoratorBase : BehaviorTreeNode
    {
        protected readonly BehaviorTreeNode _child;

        protected DecoratorBase(BehaviorTreeNode child)
        {
            _child = child;
        }
    }

    public class InverterDecoratorNode : DecoratorBase
    {
        public InverterDecoratorNode(BehaviorTreeNode child) : base(child)
        {
        }

        public override TaskStatus Tick()
        {
            var state = _child.Tick();

            return state switch
            {
                TaskStatus.Success => TaskStatus.Failure,
                TaskStatus.Failure => TaskStatus.Success,
                TaskStatus.Continue => TaskStatus.Continue,
                _ => throw new InvalidOperationException($"Unexpected TaskStatus: {state}")
            };
        }
    }

    public class RepeaterDecoratorNode : DecoratorBase
    {
        private readonly int _repeatCount;
        private int _completed;

        public RepeaterDecoratorNode(int count, BehaviorTreeNode child): base(child)
        {
            _repeatCount = count;
        }

        public override TaskStatus Tick()
        {
            var state = _child.Tick();

            // 자식이 아직 진행 중이면 그대로 대기
            if (state == TaskStatus.Continue)
            {
                return TaskStatus.Continue;
            }

            // 실패하면 즉시 종료 (정책에 따라 무시하고 카운트만 올려도 됨)
            if (state == TaskStatus.Failure)
            {
                _completed = 0;
                return TaskStatus.Failure;
            }

            // Success: 1회 완료
            _completed++;
            if (_repeatCount <= _completed)
            {
                _completed = 0;
                return TaskStatus.Success;
            }

            // 아직 더 반복해야 함 → 다음 Tick에서 자식을 다시 실행
            return TaskStatus.Continue;
        }
    }

    public static class CreateBehaviorTreeNode
    {
        /*
        public static BehaviorTreeNode BuildBehaviorTree(AiEntity entity) => new SelectorNode( // RootSelector
            new SequenceNode( // Action
                new HasValidTarget(entity),
                new ActionPrepare(entity),
                new SelectorNode( // EngageType
                    new SequenceNode( // Flee
                        new IsFleeMode(entity),
                        new MoveToTarget(entity, isRunAwayMode: true),
                        new SelectorNode( // AttackOrEnd
                            new TryAttack(entity),
                            new ActionEnd()
                        )
                    ),
                    new SequenceNode( // Chase
                        new MoveToTarget(entity, isRunAwayMode: false),
                        new SelectorNode( // AttackOrEnd
                            new TryAttack(entity),
                            new ActionEnd()
                        )
                    )
                )
            ),
            new SequenceNode( // Aggro
                new AggroReset(entity),
                new AggroSearch(entity),
                new SequenceNode( // Idle
                    new IdlePrepare(entity),
                    new IdlePatrol(entity)
                )
            )
        );
        */
    }
}
