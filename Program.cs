var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// Base de datos simulada en memoria
var coleccionNodos = new List<NodoElemento>
{
    new NodoElemento { Id = 10, Valor = "Raíz Inicial (ABB)" },
    new NodoElemento { Id = 5, Valor = "Hijo Izquierdo" }
};

// GET: Retorna todos los nodos actuales
app.MapGet("/api/nodos", () =>
{
    return Results.Ok(coleccionNodos);
});

// POST: Recibe un nuevo nodo y lo inserta en la colección
app.MapPost("/api/nodos", (NodoElemento nuevoNodo) =>
{
    if (nuevoNodo.Id <= 0 || string.IsNullOrEmpty(nuevoNodo.Valor))
    {
        return Results.BadRequest("Datos del nodo inválidos.");
    }

    foreach (var nodo in coleccionNodos)
    {
        if (nodo.Id == nuevoNodo.Id)
        {
            return Results.Conflict("Ya existe un nodo con ese ID.");
        }
    }

    coleccionNodos.Add(nuevoNodo);

    return Results.Created($"/api/nodos/{nuevoNodo.Id}", nuevoNodo);
});

app.Run();

public class NodoElemento
{
    public int Id { get; set; }
    public string Valor { get; set; } = string.Empty;
}
