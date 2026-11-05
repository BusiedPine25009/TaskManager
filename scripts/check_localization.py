#!/usr/bin/env python3
"""Проверяет согласованность переводов и ключей локализации проекта."""

import json
import re
from pathlib import Path


# Вычисляем корень относительно самого скрипта, чтобы запуск не зависел
# от текущего каталога терминала и одинаково работал в CI и локально.
PROJECT_DIR = Path(__file__).resolve().parents[1]
LANGUAGES = ("ru", "en", "kk")


def load_translations():
    """Загружает три JSON-словаря и проверяет, что каждый корневой элемент — объект."""
    translations = {}
    for language in LANGUAGES:
        path = PROJECT_DIR / "Assets" / "Localization" / f"{language}.json"
        with path.open(encoding="utf-8") as source:
            dictionary = json.load(source)
        if not isinstance(dictionary, dict) or not all(
            isinstance(key, str) and isinstance(value, str)
            for key, value in dictionary.items()
        ):
            raise ValueError(f"Словарь {path} должен содержать пары «строка → строка».")
        translations[language] = dictionary
    return translations


def find_code_keys():
    """Извлекает строковые ключи Loc.T/Loc.Plural и индексаторные ключи из XAML."""
    keys = set()
    csharp_pattern = re.compile(
        r'(?:Loc\.(?:T|Plural)|ShowToast)\(\s*"([A-Za-z0-9_.]+)"'
    )
    xaml_pattern = re.compile(r"\[([A-Za-z][A-Za-z0-9_.]*)\]")
    for suffix, pattern in (("*.cs", csharp_pattern), ("*.axaml", xaml_pattern)):
        for source_path in PROJECT_DIR.rglob(suffix):
            if any(part in {"bin", "obj", ".git"} for part in source_path.parts):
                continue
            keys.update(pattern.findall(source_path.read_text(encoding="utf-8-sig")))
    return keys


def check_plural_forms(translations):
    """Убеждается, что формы времени соответствуют ожидаемому числу для каждого языка."""
    expected = {"ru": 3, "en": 2, "kk": 1}
    for language, forms_count in expected.items():
        for key in ("time.day", "time.hour", "time.minute"):
            actual = len(translations[language][key].split("|"))
            if actual != forms_count:
                raise ValueError(
                    f"{language}.{key}: ожидалось форм {forms_count}, найдено {actual}."
                )


def select_plural(language, phrase, number):
    """Повторяет правило выбора формы локализатора для контрольных примеров словаря."""
    forms = phrase.split("|")
    if len(forms) == 1:
        return forms[0]
    if language == "kk":
        index = 0
    elif language == "en":
        index = 0 if number == 1 else 1
    else:
        last_two = abs(number) % 100
        last_digit = last_two % 10
        index = 2 if 11 <= last_two <= 14 else 0 if last_digit == 1 else 1 if 2 <= last_digit <= 4 else 2
    return forms[min(index, len(forms) - 1)]


def check_plural_examples(translations):
    """Проверяет требуемые для сдачи формы слов «день/day/күн»."""
    examples = {
        ("ru", 1): "день",
        ("ru", 2): "дня",
        ("ru", 5): "дней",
        ("en", 1): "day",
        ("en", 2): "days",
        ("kk", 1): "күн",
        ("kk", 2): "күн",
        ("kk", 5): "күн",
    }
    for (language, number), expected in examples.items():
        actual = select_plural(language, translations[language]["time.day"], number)
        if actual != expected:
            raise ValueError(
                f"Неверная форма time.day для {language}, {number}: "
                f"ожидалось «{expected}», получено «{actual}»."
            )


def check_format_arguments(translations):
    """Проверяет, что наборы плейсхолдеров каждой строки совпадают между языками."""
    token_pattern = re.compile(r"\{(\d+)\}")
    for key in translations["ru"]:
        expected = token_pattern.findall(translations["ru"][key])
        for language in ("en", "kk"):
            actual = token_pattern.findall(translations[language][key])
            if actual != expected:
                raise ValueError(
                    f"Плейсхолдеры ключа {key} различаются: ru={expected}, "
                    f"{language}={actual}."
                )


def main():
    """Запускает проверки ключей, форм множественного числа и форматных аргументов."""
    translations = load_translations()
    key_sets = {language: set(dictionary) for language, dictionary in translations.items()}
    reference_keys = key_sets["ru"]
    for language, keys in key_sets.items():
        if keys != reference_keys:
            raise ValueError(
                f"Набор ключей {language} не совпадает с ru: "
                f"отсутствуют {sorted(reference_keys - keys)}, "
                f"лишние {sorted(keys - reference_keys)}."
            )

    missing = find_code_keys() - reference_keys
    if missing:
        raise ValueError(f"В словарях отсутствуют ключи, используемые кодом: {sorted(missing)}")

    check_plural_forms(translations)
    check_plural_examples(translations)
    check_format_arguments(translations)
    print(f"Проверка локализации пройдена: {len(reference_keys)} ключей, ru/en/kk.")


if __name__ == "__main__":
    main()
