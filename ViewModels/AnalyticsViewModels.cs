using System;
using System.Globalization;
using Avalonia.Media;

namespace TaskManager.ViewModels;

/// <summary>Данные одного столбца графика завершений.</summary>
public sealed class CompletionBarViewModel
{
    /// <summary>Создаёт столбец с ограниченной высотой для компактного графика.</summary>
    public CompletionBarViewModel(DateTime date, int count, int maximum)
    {
        Label = date.ToString("dd.MM");
        Count = count;
        Height = maximum == 0 ? 5 : Math.Max(5, Math.Round(count * 116d / maximum));
    }

    /// <summary>Подпись даты под столбцом.</summary>
    public string Label { get; }
    /// <summary>Число выполненных задач в этот день.</summary>
    public int Count { get; }
    /// <summary>Высота столбца в пикселях.</summary>
    public double Height { get; }
}

/// <summary>Сектор круговой диаграммы статусов.</summary>
public sealed class DonutSegmentViewModel
{
    /// <summary>Создаёт сектор по доле; при нулевой доле геометрия остаётся пустой.</summary>
    public DonutSegmentViewModel(double start, double fraction, string color)
    {
        Color = color;
        if (fraction <= 0) return;
        double end = start + fraction * Math.PI * 2;
        if (fraction >= .9999)
        {
            PathData = "M 70,5 A 65,65 0 1 1 69.99,5 Z";   // полный круг: одна дуга не рисуется, поэтому две
            return;
        }
        (double x1, double y1) = Point(start);
        (double x2, double y2) = Point(end);
        int largeArc = fraction > .5 ? 1 : 0;
        // ВАЖНО: числа в строке геометрии должны быть с точкой. В русской культуре (ru-RU) дробная часть
        // записывается через запятую, и строка превращается в мусор («M 70,70 L 123,45,67,89»),
        // поэтому форматируем в инвариантной культуре.
        PathData = string.Create(CultureInfo.InvariantCulture,
            $"M 70,70 L {x1:0.##},{y1:0.##} A 65,65 0 {largeArc} 1 {x2:0.##},{y2:0.##} Z");
    }

    /// <summary>Цвет сектора из палитры, пригодный для обеих тем.</summary>
    public string Color { get; }
    /// <summary>SVG-подобная геометрия сектора, понимаемая Avalonia Path.</summary>
    public string PathData { get; } = string.Empty;

    /// <summary>Рассчитывает точку на окружности радиусом 65.</summary>
    private static (double X, double Y) Point(double angle)
        => (70 + 65 * Math.Sin(angle), 70 - 65 * Math.Cos(angle));
}

/// <summary>Строка распределения задач по приоритету.</summary>
public sealed class PriorityBarViewModel
{
    /// <summary>Создаёт строку с долей от наибольшего значения.</summary>
    public PriorityBarViewModel(string label, int count, int maximum, string color)
    {
        Label = label;
        Count = count;
        Color = color;
        Width = maximum == 0 ? 0 : Math.Max(4, Math.Round(count * 190d / maximum));
    }

    /// <summary>Локализованное название приоритета.</summary>
    public string Label { get; }
    /// <summary>Число задач.</summary>
    public int Count { get; }
    /// <summary>Ширина индикатора распределения.</summary>
    public double Width { get; }
    /// <summary>Цвет индикатора.</summary>
    public string Color { get; }
}

/// <summary>Строка легенды круговой диаграммы: цветная точка, название и количество.</summary>
public sealed class LegendItemViewModel
{
    /// <summary>Создаёт строку легенды.</summary>
    public LegendItemViewModel(string label, int count, string color)
    {
        Label = label;
        Count = count;
        Color = color;
    }

    /// <summary>Локализованное название статуса.</summary>
    public string Label { get; }
    /// <summary>Число задач с этим статусом.</summary>
    public int Count { get; }
    /// <summary>Цвет точки (совпадает с цветом сектора).</summary>
    public string Color { get; }
}
