using Unity.Entities;

namespace AreaBucket.Utils
{
    public interface IHandleUpdatable
    {

        void AssignHandle(ref SystemState state);

        void UpdateHandle(ref SystemState state);
    }
}