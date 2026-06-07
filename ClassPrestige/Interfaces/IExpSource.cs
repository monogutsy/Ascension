using ClassPrestige.Managers;

namespace ClassPrestige.Interfaces;
public interface IExpSource
{
    string Name { get; }
    void Register(ExpManager manager);
    int CalculateBaseExp(object context);
}
