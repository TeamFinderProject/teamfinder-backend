using TeamFinder.Api.Entities;

namespace TeamFinder.Api.Data;

/// <summary>Справочники наполняет команда, пользователи их не пополняют (RULE-PROF-01).</summary>
public static class SeedData
{
    public static readonly Skill[] Skills =
    [
        new() { Id = 1, Name = "C#", Description = "Язык программирования" },
        new() { Id = 2, Name = "ASP.NET Core", Description = "Платформа серверных приложений" },
        new() { Id = 3, Name = "PostgreSQL", Description = "Реляционная база данных" },
        new() { Id = 4, Name = "Entity Framework Core", Description = "ORM для .NET" },
        new() { Id = 5, Name = "JavaScript", Description = "Язык программирования" },
        new() { Id = 6, Name = "TypeScript", Description = "Типизированный JavaScript" },
        new() { Id = 7, Name = "React", Description = "Библиотека пользовательских интерфейсов" },
        new() { Id = 8, Name = "HTML/CSS", Description = "Разметка и стилизация веб-страниц" },
        new() { Id = 9, Name = "Docker", Description = "Контейнеризация приложений" },
        new() { Id = 10, Name = "Git", Description = "Контроль версий" },
        new() { Id = 11, Name = "Go", Description = "Язык программирования" },
        new() { Id = 12, Name = "Redis", Description = "Хранилище данных в памяти" },
        new() { Id = 13, Name = "Figma", Description = "Проектирование интерфейсов" },
        new() { Id = 14, Name = "Python", Description = "Язык программирования" },
        new() { Id = 15, Name = "REST API", Description = "Проектирование и разработка API" },
        new() { Id = 16, Name = "SQL", Description = "Язык запросов к реляционным БД" },
        new() { Id = 17, Name = "Тестирование ПО", Description = "Проверка качества программ" },
        new() { Id = 18, Name = "UI/UX", Description = "Проектирование пользовательского опыта" },
    ];

    public static readonly RoleType[] RoleTypes =
    [
        new() { Id = 1, Name = "Backend Developer", Description = "Разработчик серверной части" },
        new() { Id = 2, Name = "Frontend Developer", Description = "Разработчик пользовательского интерфейса" },
        new() { Id = 3, Name = "Fullstack Developer", Description = "Разработчик серверной и клиентской частей" },
        new() { Id = 4, Name = "Designer", Description = "Дизайнер" },
        new() { Id = 5, Name = "DevOps Engineer", Description = "Инженер инфраструктуры и развёртывания" },
        new() { Id = 6, Name = "QA Engineer", Description = "Инженер по тестированию" },
        new() { Id = 7, Name = "Product Manager", Description = "Менеджер продукта" },
        new() { Id = 8, Name = "Data Analyst", Description = "Аналитик данных" },
    ];
}
