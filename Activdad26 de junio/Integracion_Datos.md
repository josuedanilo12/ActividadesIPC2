# Integración de Datos

**Actividad de Laboratorio: Interoperabilidad y Carga Masiva de Datos**

## Parte 1: Evaluación Conceptual y Buenas Prácticas

### 1. Formatos de intercambio

| Formato | Ventajas | Desventajas |
|---|---|---|
| **CSV** | Es ligero, fácil de generar, leer y procesar. Ocupa poco espacio y es adecuado para grandes volúmenes de datos tabulares. | No maneja estructuras jerárquicas de forma natural, no incluye tipos de datos y puede presentar problemas cuando los campos contienen comas, saltos de línea o caracteres especiales. |
| **XML** | Permite representar información estructurada y jerárquica. Es extensible, autodescriptivo y puede validarse mediante esquemas como XSD. | Es más pesado y verboso que CSV, requiere mayor espacio de almacenamiento y su procesamiento suele consumir más memoria y tiempo. |

### 2. Diferencia entre serialización y deserialización

La **serialización** es el proceso de convertir un objeto de C# a un formato de texto, por ejemplo JSON, para almacenarlo o enviarlo mediante una API.

Con `System.Text.Json` se puede realizar de la siguiente manera:

```csharp
string json = JsonSerializer.Serialize(alumno);
```

La **deserialización** es el proceso contrario: convierte un texto JSON en un objeto de C# para poder utilizar sus propiedades dentro del programa.

```csharp
Alumno? alumno = JsonSerializer.Deserialize<Alumno>(json);
```

En resumen:

- Serialización: objeto de C# → JSON.
- Deserialización: JSON → objeto de C#.

### 3. Antipatrón de rendimiento N+1

El problema **N+1** ocurre cuando se ejecuta una consulta o una inserción individual por cada registro leído de un archivo masivo.

Por ejemplo, si un archivo contiene 1,000 alumnos y se llama a `SaveChangesAsync()` dentro del ciclo, se podrían realizar 1,000 operaciones separadas contra la base de datos. Esto provoca más conexiones, más transacciones y un tiempo de ejecución mucho mayor.

La solución consiste en aplicar **procesamiento por lotes o batching**:

1. Leer y transformar todos los registros.
2. Guardarlos temporalmente en una colección.
3. Agregarlos mediante `AddRange()`.
4. Ejecutar una sola llamada a `SaveChangesAsync()` al finalizar.

De esta manera se reduce la cantidad de operaciones contra la base de datos y se mejora el rendimiento.

---

## Parte 2: Implementación Práctica en C#

## Modelo Alumno

El siguiente modelo será utilizado en los dos desafíos:

```csharp
public class Alumno
{
    public int Id { get; set; }
    public string Carnet { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string Correo { get; set; } = string.Empty;
    public string Carrera { get; set; } = string.Empty;
}
```

## Desafío 1: Consumo de Endpoints y Deserialización

El siguiente método utiliza `HttpClient` para realizar una petición asíncrona de tipo GET. También valida el código de estado, maneja excepciones y deserializa el contenido JSON a un objeto de tipo `Alumno`.

```csharp
using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

public class AlumnoService
{
    private readonly HttpClient _httpClient;

    public AlumnoService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<Alumno?> ObtenerAlumnoAsync()
    {
        const string url = "https://api.usac.edu/v1/alumnos";

        try
        {
            HttpResponseMessage respuesta =
                await _httpClient.GetAsync(url);

            respuesta.EnsureSuccessStatusCode();

            string contenidoJson =
                await respuesta.Content.ReadAsStringAsync();

            JsonSerializerOptions opciones = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            Alumno? alumno =
                JsonSerializer.Deserialize<Alumno>(
                    contenidoJson,
                    opciones
                );

            return alumno;
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine(
                $"Error al realizar la petición HTTP: {ex.Message}"
            );

            return null;
        }
        catch (JsonException ex)
        {
            Console.WriteLine(
                $"Error al deserializar el JSON: {ex.Message}"
            );

            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"Ocurrió un error inesperado: {ex.Message}"
            );

            return null;
        }
    }
}
```

> Si el endpoint devuelve un arreglo de alumnos en lugar de un solo alumno, el tipo de retorno puede cambiarse a `List<Alumno>` y la deserialización puede realizarse con `JsonSerializer.Deserialize<List<Alumno>>()`.

## Desafío 2: Endpoint para Carga Masiva CSV

El siguiente controlador recibe un archivo CSV mediante `IFormFile`, lo procesa línea por línea con `StreamReader` y almacena los registros en una lista intermedia.

La inserción se realiza al final mediante `AddRange()` y una única llamada a `SaveChangesAsync()`.

### Ejemplo de estructura del archivo CSV

```csv
Carnet,Nombre,Correo,Carrera
202343544,Josué Colindres,jcolindres@usac.edu.gt,Ingeniería en Ciencias y Sistemas
202300001,Ana López,alopez@usac.edu.gt,Ingeniería Industrial
```

### Código del endpoint

```csharp
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

[ApiController]
[Route("api/[controller]")]
public class AlumnosController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public AlumnosController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpPost("carga-masiva")]
    public async Task<IActionResult> CargarAlumnosCsv(
        IFormFile archivo
    )
    {
        if (archivo == null || archivo.Length == 0)
        {
            return BadRequest(
                "Debe seleccionar un archivo CSV válido."
            );
        }

        string extension =
            Path.GetExtension(archivo.FileName).ToLower();

        if (extension != ".csv")
        {
            return BadRequest(
                "El archivo debe tener extensión .csv."
            );
        }

        List<Alumno> alumnos = new List<Alumno>();

        try
        {
            using Stream stream = archivo.OpenReadStream();

            using StreamReader lector =
                new StreamReader(stream);

            bool primeraLinea = true;
            int numeroLinea = 0;

            while (!lector.EndOfStream)
            {
                string? linea =
                    await lector.ReadLineAsync();

                numeroLinea++;

                if (string.IsNullOrWhiteSpace(linea))
                {
                    continue;
                }

                if (primeraLinea)
                {
                    primeraLinea = false;
                    continue;
                }

                string[] columnas = linea.Split(',');

                if (columnas.Length < 4)
                {
                    return BadRequest(
                        $"La línea {numeroLinea} no tiene el formato esperado."
                    );
                }

                Alumno alumno = new Alumno
                {
                    Carnet = columnas[0].Trim(),
                    Nombre = columnas[1].Trim(),
                    Correo = columnas[2].Trim(),
                    Carrera = columnas[3].Trim()
                };

                alumnos.Add(alumno);
            }

            if (alumnos.Count == 0)
            {
                return BadRequest(
                    "El archivo no contiene registros para importar."
                );
            }

            _context.Alumnos.AddRange(alumnos);

            await _context.SaveChangesAsync();

            return Ok(new
            {
                mensaje = "Carga masiva completada correctamente.",
                registrosInsertados = alumnos.Count
            });
        }
        catch (IOException ex)
        {
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                $"Error al leer el archivo: {ex.Message}"
            );
        }
        catch (DbUpdateException ex)
        {
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                $"Error al guardar los datos: {ex.Message}"
            );
        }
        catch (Exception ex)
        {
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                $"Ocurrió un error inesperado: {ex.Message}"
            );
        }
    }
}
```

## Contexto de base de datos utilizado en el ejemplo

```csharp
using Microsoft.EntityFrameworkCore;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options
    ) : base(options)
    {
    }

    public DbSet<Alumno> Alumnos { get; set; }
}
```

## Explicación del rendimiento del endpoint

El endpoint evita cargar el archivo completo en memoria porque utiliza `StreamReader` y `ReadLineAsync()` para procesarlo línea por línea.

Aunque los objetos ya transformados se guardan temporalmente en una lista, no se ejecuta una inserción individual dentro del ciclo. Todos los alumnos se agregan al contexto con `AddRange()` y se confirma la operación con una sola llamada a `SaveChangesAsync()`.

Esto evita el problema N+1 y reduce la cantidad de operaciones realizadas contra la base de datos.

---

## Parte 3: Referencia Bibliográfica

Facultad de Ingeniería, USAC. (2026). *Sesión 20: Integración de Datos. Consumo de APIs Externas y Carga Masiva (CSV/XML).* Laboratorio del curso Introducción a la Programación y Computación 2. Guatemala.
