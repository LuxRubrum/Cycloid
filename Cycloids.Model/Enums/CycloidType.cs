namespace Cycloids.Model.Enums;

/// <summary> Тип траектории точки, жестко связанной с катящимся колесом </summary>
public enum CycloidType
{
    /// <summary> Циклоида где d < R: точка внутри колеса </summary>
    Curtate,

    /// <summary> Обыкновенная циклоида d = R: точка на ободе </summary>
    Common,

    /// <summary> Удлиненная циклоида d > R: точка за пределами обода </summary>
    Prolate
}
