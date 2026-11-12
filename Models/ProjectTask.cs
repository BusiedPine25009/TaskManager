using System;
using TaskManager.Services;

namespace TaskManager.Models;

/// <summary>
/// Задача, относящаяся к определённому проекту (например, «Курсовая работа»).
/// Позволяет группировать задачи по проектам.
/// </summary>
public class ProjectTask : TaskItem
{
    // Название проекта (закрытое поле, доступ через свойство с проверкой).
    private string _projectName = Loc.T("project.defaultName");

    /// <summary>Название проекта. Не может быть пустым.</summary>
    public string ProjectName
    {
        get => _projectName;
        set
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException(Loc.T("err.project.empty"));

            _projectName = value.Trim();
        }
    }

    /// <summary>Название вида задачи (переопределение базового метода).</summary>
    public override string GetTypeName() => Loc.T("enum.type.project");

    /// <summary>Дополняет базовое описание названием проекта.</summary>
    public override string GetDetails()
    {
        return $"{base.GetDetails()} | {Loc.T("card.project", ProjectName)}";
    }
}
