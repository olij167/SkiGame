# Module Lifecycle Management Migration Guide

## Overview

The new Module Lifecycle Management system provides a robust framework for managing module initialization, startup, shutdown, and restart processes. This guide will help you migrate your existing modules to use the new system and eliminate memory leaks and null reference errors.

## Key Benefits

- **Proper Cleanup**: Automatic event unsubscription prevents memory leaks
- **Graceful Restart**: Complete system restart capability without editor restart
- **Error Handling**: Safe module operation with proper error tracking
- **State Management**: Clear module lifecycle states for debugging
- **Hot Reload**: Modules can be stopped, updated, and restarted without losing data

## Migration Steps

### 1. Update Your Module Class

**Before (Old System):**

```csharp
public class MyModule
{
    static bool isStarted = false;

    static void StartModule()
    {
        // Initialization mixed with startup
        LoadData();

        // Event subscription with potential duplicates
        EditorApplication.update += OnEditorUpdate;
        StartModules += OnStartModules;
    }

    static void OnSceneClosing()
    {
        // Incomplete cleanup
        isStarted = false;
    }
}
```

**After (New System):**

```csharp
public class MyModule : ModuleBase
{
    public override string ModuleId => "com.yourcompany.mymodule";
    public override string ModuleName => "My Module";

    [InitializeOnLoadMethod]
    static void RegisterModule()
    {
        MightyCore.RegisterModule(new MyModule());
    }

    protected override void OnInitialize()
    {
        // Load data and setup references only
        LoadData();
    }

    protected override void OnStart()
    {
        // Subscribe to events safely
        SafeSubscribeEditorCallback(ref EditorApplication.update, OnEditorUpdate);
        SafeSubscribe(ref StartModules, OnStartModules);
    }

    protected override void OnStop()
    {
        // Stop processing (events auto-unsubscribed)
        StopProcessing();
    }

    protected override void OnShutdown()
    {
        // Complete cleanup
        CleanupData();
    }
}
```

### 2. Implement Required Abstract Methods

Your module must implement these four lifecycle methods:

```csharp
protected override void OnInitialize()
{
    // Load configuration, setup data structures
    // Do NOT subscribe to events here
}

protected override void OnStart()
{
    // Subscribe to events using SafeSubscribe
    // Start processing, enable functionality
}

protected override void OnStop()
{
    // Stop processing, disable functionality
    // Events are automatically unsubscribed
}

protected override void OnShutdown()
{
    // Complete cleanup, free resources
    // Clear all data
}
```

### 3. Use Safe Event Subscription

**Old Way (Causes Memory Leaks):**

```csharp
EditorApplication.update -= OnUpdate;
EditorApplication.update += OnUpdate;
```

**New Way (Automatic Cleanup):**

```csharp
// For EditorApplication.CallbackFunction events
SafeSubscribeEditorCallback(ref EditorApplication.update, OnUpdate);

// For Action events (Mighty core events)
SafeSubscribe(ref StartModules, OnStartModules);
SafeSubscribe(ref Rebuild, OnRebuild);

// For Unity scene events (special handling required)
SafeSubscribeSceneOpened(OnSceneOpened);
SafeSubscribeSceneClosing(OnSceneClosing);
```

**Note on SafeSubscribe Methods:**

- `SafeSubscribe(ref Action eventAction, Action handler)` - For Mighty core Action events
- `SafeSubscribeEditorCallback(ref EditorApplication.CallbackFunction eventAction, EditorApplication.CallbackFunction handler)` - For Unity editor callbacks
- Custom helper methods like `SafeSubscribeSceneOpened()` - For Unity events that can't be passed by reference

### 4. Module Registration

Add this static method to automatically register your module:

```csharp
[InitializeOnLoadMethod]
static void RegisterModule()
{
    var module = new YourModule();
    MightyCore.RegisterModule(module);
}
```

## Common Patterns

### Data Loading and Saving

```csharp
private YourModuleData moduleData;

protected override void OnInitialize()
{
    // Load data during initialization
    moduleData = YourModuleData.Load();
}

protected override void OnShutdown()
{
    // Save data during shutdown
    if (moduleData != null)
    {
        moduleData.Save();
        moduleData = null;
    }
}
```

### Unity Component Management

```csharp
private YourComponent component;

protected override void OnStart()
{
    // Find or create components
    var anchor = GameObject.Find("MightySceneAnchor");
    if (anchor != null)
    {
        component = anchor.GetComponent<YourComponent>();
        if (component == null)
            component = anchor.AddComponent<YourComponent>();
    }
}

protected override void OnStop()
{
    // Disable component
    if (component != null)
        component.enabled = false;
}

protected override void OnShutdown()
{
    // Destroy component
    if (component != null)
    {
        GameObject.DestroyImmediate(component);
        component = null;
    }
}
```

### Scene Event Handling

```csharp
protected override void OnStart()
{
    // Use helper methods for Unity scene events
    SafeSubscribeSceneOpened(OnSceneOpened);
    SafeSubscribeSceneClosing(OnSceneClosing);
}

// Add helper methods for Unity scene events
private void SafeSubscribeSceneOpened(EditorSceneManager.SceneOpenedCallback handler)
{
    EditorSceneManager.sceneOpened -= handler;
    EditorSceneManager.sceneOpened += handler;
    eventUnsubscribers.Add(() => EditorSceneManager.sceneOpened -= handler);
}

private void SafeSubscribeSceneClosing(EditorSceneManager.SceneClosingCallback handler)
{
    EditorSceneManager.sceneClosing -= handler;
    EditorSceneManager.sceneClosing += handler;
    eventUnsubscribers.Add(() => EditorSceneManager.sceneClosing -= handler);
}
```

## System-Wide Operations

### Restart All Modules

```csharp
// Graceful restart of entire system
MightyCore.GracefulSystemRestart();

// Or restart individual modules
MightyCore.RestartAllModules();
```

### Health Monitoring

```csharp
// Check module health
var healthStatus = MightyCore.GetModuleHealthStatus();
foreach (var module in healthStatus)
{
    Debug.Log($"Module {module.Key}: {(module.Value ? "Healthy" : "Error")}");
}

// Get error details
var errors = MightyCore.GetModuleErrors();
foreach (var error in errors)
{
    Debug.LogError($"Module {error.Key}: {error.Value}");
}
```

### Module Discovery

```csharp
// Get specific module
var myModule = MightyCore.GetModule("com.yourcompany.mymodule");

// Get all modules
var allModules = MightyCore.GetAllModules();
```

## Best Practices

1. **Separate Initialization from Startup**: Use `OnInitialize()` for data loading, `OnStart()` for event subscription
2. **Always Use SafeSubscribe**: This prevents duplicate subscriptions and ensures cleanup
3. **Implement Health Checks**: Override `IsHealthy()` to provide meaningful health status
4. **Handle Errors Gracefully**: Use try-catch in your lifecycle methods and set `lastError`
5. **Clean State Management**: Always reset your module state in `OnShutdown()`

## Troubleshooting

### Common Issues

**Issue**: Module not starting
**Solution**: Check that `RegisterModule()` is called with `[InitializeOnLoadMethod]`

**Issue**: Events still firing after module stop
**Solution**: Ensure you're using `SafeSubscribe()` instead of direct event subscription

**Issue**: Memory leaks on scene transition
**Solution**: Verify all resources are cleaned up in `OnShutdown()`

**Issue**: Null reference errors
**Solution**: Check module state before accessing data: `if (State != ModuleLifecycleState.Started) return;`

### Debug Information

```csharp
// Enable debug logging
MightyCoreData.DevLogs = true;

// Check module states
foreach (var module in MightyCore.GetAllModules())
{
    Debug.Log($"{module.ModuleName}: {module.State}");
}
```

## Example Complete Migration

See `Assets/MightyDevOps/Core/Examples/ExampleModule.cs` for a complete working example of a module using the new lifecycle system.

This migration will eliminate the memory leaks and null reference errors you were experiencing, while providing a robust foundation for module management in your asset.
