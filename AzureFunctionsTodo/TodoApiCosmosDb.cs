using System.IO;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.WebJobs.Host;
using Newtonsoft.Json;
using System.Threading.Tasks;
using System.Linq;
using System;
using System.Collections.Generic;
using Microsoft.Azure.Documents.Client;

namespace AzureFunctionsTodo
{

    public static class TodoApiCosmosDb
    {
        private const string route = "todo4";

        [FunctionName("CosmosDb_CreateTodo")]
        public static async Task<IActionResult>CreateTodo(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = route)]HttpRequest req,
            [CosmosDB(
                databaseName: "tododb",
                collectionName: "tasks",
                ConnectionStringSetting = "CosmosDBConnection")]
            IAsyncCollector<object> todos, TraceWriter log)
        {
            log.Info("Creating a new todo list item");
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var input = JsonConvert.DeserializeObject<TodoCreateModel>(requestBody);

            var todo = new Todo() { TaskDescription = input.TaskDescription };
            //the object we need to add has to have a lower case id property or we'll
            // end up with a cosmosdb document with two properties - id (autogenerated) and Id
            await todos.AddAsync(new { id = todo.Id, todo.CreatedTime, todo.IsCompleted, todo.TaskDescription });
            return new OkObjectResult(todo);
        }

        [FunctionName("CosmosDb_GetTodos")]
        public static async Task<IActionResult> GetTodos(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = route)]HttpRequest req,
            [CosmosDB(
                databaseName: "tododb",
                collectionName: "tasks",
                ConnectionStringSetting = "CosmosDBConnection",
                SqlQuery = "SELECT * FROM c order by c._ts desc")]
                IEnumerable<Todo> todos,
            TraceWriter log)
        {
            log.Info("Getting todo list items");
            return new OkObjectResult(todos);
        }

        [FunctionName("CosmosDb_GetTodoById")]
        public static IActionResult GetTodoById(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = route + "/{id}")]HttpRequest req,
            [CosmosDB(databaseName: "tododb", collectionName: "tasks", ConnectionStringSetting = "CosmosDBConnection",
                Id = "{id}")] Todo todo,
            TraceWriter log, string id)
        {
            log.Info("Getting todo item by id");

            if (todo == null)
            {
                log.Info($"Item {id} not found");
                return new NotFoundResult();
            }
            return new OkObjectResult(todo);
        }

        [FunctionName("CosmosDb_UpdateTodo")]
        public static async Task<IActionResult> UpdateTodo(
            [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = route + "/{id}")]HttpRequest req,
            [CosmosDB(ConnectionStringSetting = "CosmosDBConnection")]
                DocumentClient client,
            TraceWriter log, string id)
        {
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var updated = JsonConvert.DeserializeObject<TodoUpdateModel>(requestBody);
            Uri collectionUri = UriFactory.CreateDocumentCollectionUri("tododb", "tasks");
            var document = client.CreateDocumentQuery(collectionUri).Where(t => t.Id == id)
                            .AsEnumerable().FirstOrDefault();
            if (document == null)
            {
                return new NotFoundResult();
            }
            

            document.SetPropertyValue("IsCompleted", updated.IsCompleted);
            if (!string.IsNullOrEmpty(updated.TaskDescription))
            {
                document.SetPropertyValue("TaskDescription", updated.TaskDescription);
            }

            await client.ReplaceDocumentAsync(document);

            /* var todo = new Todo()
            {
                Id = document.GetPropertyValue<string>("id"),
                CreatedTime = document.GetPropertyValue<DateTime>("CreatedTime"),
                TaskDescription = document.GetPropertyValue<string>("TaskDescription"),
                IsCompleted = document.GetPropertyValue<bool>("IsCompleted")
            };*/

            // an easier way to deserialize a Document
            Todo todo2 = (dynamic)document;

            return new OkObjectResult(todo2);
        }

        [FunctionName("CosmosDb_DeleteTodo")]
        public static async Task<IActionResult> DeleteTodo(
            [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = route + "/{id}")]HttpRequest req,
            [CosmosDB(ConnectionStringSetting = "CosmosDBConnection")] DocumentClient client,
            TraceWriter log, string id)
        {
            Uri collectionUri = UriFactory.CreateDocumentCollectionUri("tododb", "tasks");
            var document = client.CreateDocumentQuery(collectionUri).Where(t => t.Id == id)
                    .AsEnumerable().FirstOrDefault();
            if (document == null)
            {
                return new NotFoundResult();
            }
            await client.DeleteDocumentAsync(document.SelfLink);
            return new OkResult();
        }
    }
}
