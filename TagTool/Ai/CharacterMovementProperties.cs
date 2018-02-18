using TagTool.Serialization;
using System.Collections.Generic;

namespace TagTool.Ai
{
    [TagStructure(Size = 0x2C)]
    public class CharacterMovementProperties
    {
        public CharacterMovementFlags Flags;
        public float PathfindingRadius;
        public float DestinationRadius;
        public float DiveGrenadeChance;
        public AiSize ObstacleLeapMinimumSize;
        public AiSize ObstacleLeapMaximumSize;
        public AiSize ObstacleIgnoreSize;
        public AiSize ObstaceSmashableSize;
        public CharacterJumpHeight JumpHeight;
        public CharacterMovementHintFlags HintFlags;
        public List<CharacterChangeDirectionPause> ChangeDirectionPause;
    }
}