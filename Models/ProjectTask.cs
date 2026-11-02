using System;

namespace TaskManager.Models;

/// <summary>
/// Задача, относящаяся к определённому проекту (например, «Курсовая работа»).
/// Позволяет группировать задачи по проектам.
/// </summary>
public class ProjectTask : TaskItem
{
    // Название проекта (закрытое поле, доступ через свойство с проверкой).
    private string _projectName = "Без проекта";

    /// <summary>Название проекта. Не может быть пустым.</summary>
    public string ProjectName
    {
        get => _projectName;
        set
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Название проекта не может быть пустым.");

            _projectName = value.Trim();
        }
    }

    /// <summary>Название вида задачи (переопределение базового метода).</summary>
    public override string GetTypeName() => "Проектная";

    /// <summary>Дополняет базовое описание названием проекта.</summary>
    public override string GetDetails()
    {
        return $"{base.GetDetails()} | проект: {ProjectName}";
    }
}
