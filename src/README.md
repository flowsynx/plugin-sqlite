## FlowSynx SQLite Plugin

The SQLite Plugin is a pre-packaged, plug-and-play integration component for the FlowSynx engine. It enables executing SQLite queries with configurable parameters such as database file paths, SQL templates, and runtime parameters. Designed for FlowSynx’s no-code/low-code automation workflows, this plugin simplifies database integration, data retrieval, and transformation tasks for lightweight, file-based databases.

This plugin is automatically installed by the FlowSynx engine when selected within the platform. It is not intended for manual installation or standalone developer use outside the FlowSynx environment.

---

## Purpose

The SQLite Plugin allows FlowSynx users to:

- Execute parameterized SQL commands securely.
- Retrieve data from SQLite and pass it downstream in workflows.
- Perform data transformation and filtering inline using SQL.
- Integrate SQLite operations into automation workflows without writing code.

---

## Supported Operations

- **query**: Executes a SQL `SELECT` query and returns the result set as JSON.
- **execute**: Executes a SQL command (`INSERT`, `UPDATE`, `DELETE`, etc.) and returns the number of affected rows.

---

## Plugin Specifications

The plugin requires the following configuration:
- ConnectionString (string): **Required.** The PostgreSQL connection string used to connect to the database. Example:
```
Data Source=C:\databases\flowdata.db
```

---

## Input Parameters

The plugin accepts the following parameters:

- `Operation` (string): **Required.** The type of operation to perform. Supported values are `query` and `execute`.  
- `Sql` (string): **Required.** The SQL query or command to execute. Use parameter placeholders (e.g., `@id`, `@name`) for dynamic values.  
- `Params` (object): Optional. A dictionary of parameter names and values to be used in the SQL template.

### Example input

```json
{
  "Operation": "query",
  "Sql": "SELECT id, name, email FROM users WHERE country = @country",
  "Parameters": {
    "country": "Norway"
  }
}
```

---

## Debugging Tips

- Ensure the `ConnectionString` is correct and the file is accessible from the FlowSynx environment.  
- Use parameter placeholders (`@parameterName`) in the SQL to prevent SQL injection and enable parameterization.  
- Validate that all required parameters are provided in the `Params` dictionary.  
- If a query returns no results, verify that your SQL `WHERE` conditions are correct and the target table contains matching data. 

---

## SQLite Limitations

When using SQLite within FlowSynx, keep the following considerations in mind:

- **No Parallel Writes**: SQLite allows only one write operation at a time. If multiple workflows attempt to write simultaneously, you may encounter locking errors. Consider queueing or serializing writes in high-concurrency scenarios.  
- **File-based Database**: SQLite databases are single files. Ensure the file is on a filesystem accessible to the FlowSynx runtime.  
- **Data Type Affinity**: SQLite uses dynamic typing (data type affinity), meaning values are stored based on the content rather than strict column types. Validate data formats explicitly when interacting with other systems.  
- **Size and Performance**: SQLite is best suited for lightweight workloads. For large datasets or heavy concurrent access, consider using a server-based database (e.g., PostgreSQL, MySQL).  
- **In-Memory Databases**: If using an in-memory database (`:memory:`), the database contents exist only during the lifetime of the plugin execution and will not persist across operations.  

---

## Security Notes

- SQL commands are executed using parameterized queries to prevent SQL injection.  
- The plugin does not store credentials or data outside of execution unless explicitly configured.  
- Only authorized FlowSynx platform users can view or modify configurations. 

---

## License

© FlowSynx. All rights reserved.