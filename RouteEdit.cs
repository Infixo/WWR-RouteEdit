#pragma warning disable CA1416 // Validate platform compatibility

using HarmonyLib;
using STM.GameWorld;
using STM.GameWorld.Commands;
using STM.GameWorld.Users;
using STMG.Engine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;
using Utilities;

namespace RouteEdit;


[HarmonyPatch]
public static class RouteEdit
{
    [HarmonyPatch(typeof(CommandChangeRoute), "Apply"), HarmonyPrefix]
    public static bool CommandChangeRoute_Apply_Prefix(CommandChangeRoute __instance, GameScene scene)
    {
        if (__instance.Hub_city == null)
        {
            Log.Write("Warning! Hub is null!");
            return false;
        }

        // Command fields
        VehicleBaseUser vehicle = __instance.Vehicle;
        NewRouteSettings settings = __instance.GetPrivateField<NewRouteSettings>("settings");

        // Store reference cities
        byte vehicleState = vehicle.Route.Loading ? (byte)1 : (byte)0;
        CityUser current = vehicle.Route.Current;
        CityUser destination = vehicle.Route.Destination;

        Hub hub = __instance.Hub_city.GetHub(__instance.Company);
        Company company = scene.Session.Companies[__instance.Company];
        company.Line_manager.RemoveVehicle(__instance.Vehicle);

        if (vehicle != null && hub != null && !vehicle.Destroyed && vehicle.Company == __instance.Company)
        {
            //__instance.Vehicle.ChangeRoute(__instance.GetPrivateField<NewRouteSettings>("settings"), _hub, scene);
            Company _company = __instance.Vehicle.GetCompany(scene);
            if (!vehicle.Route.Loading)
            {
                __instance.Vehicle.Passengers.RemoveAndRefund(vehicle.Route, _company);
            }
            else
            {
                __instance.Vehicle.Passengers.RemovePassengers();
            }

            long _import = __instance.Vehicle.GetImportCost(scene.Cities[hub.City].User, scene);
            if (_import > 0)
            {
                _company.AddExpense(_import, vehicle);
            }

            __instance.Vehicle.Route.Destroy();
            //Route = new RouteInstance(new Route(settings), __instance.Vehicle, scene);
            vehicle.SetPublicProperty("Route", new RouteInstance(new Route(settings), vehicle, scene)); // this attaches a new route instance to required cities

            // Move to a new hub if changed
            if (__instance.Vehicle.Hub != hub)
            {
                vehicle.Hub.Vehicles.Remove(vehicle);
                vehicle.SetPublicProperty("Hub", hub);
                vehicle.Hub.Vehicles.Add(vehicle);
            }

        }
        else
            Log.Write($"Warning! Logic issue in command apply! v={vehicle} h={hub} d={vehicle?.Destroyed} c={vehicle?.Company}");

        // TODO: This will create a new line if for some reason the currently edited line cannot be used!
        company.Line_manager.AddVehicleToLine(vehicle, scene);

        if (__instance.Company == scene.Session.Player)
            scene.Session.Commands.vehicles_changed = true;

        // hm, this will also set new RouteCycle effectively moving a vehicle
        // I need a tweak here to set new params

        CityUser? newCurrent = vehicle?.Route.Instructions.FindClosest(current, vehicle is ShipUser);

        if (newCurrent == null)
        {
            Log.Write($"No replacement for the current city {current.Name}.");
            return false;
        }
        if (newCurrent != current && WorldwideRushExtensions.GetDistance(newCurrent, current, vehicle!.Entity_base.Type_name) == 0)
        {
            Log.Write($"There is no road/rails/sea path between {current.Name} and {newCurrent.Name}.");
            return false;
        }

        CityUser? newDestination = vehicle?.Route.Instructions.FindClosest(destination, vehicle is ShipUser, newCurrent);
        if (newDestination == null)
        {
            Log.Write($"No replacement for the destination city {current.Name}.");
            return false;
        }

        // Here we have both newCurrent and newDestination, and both are ok
        // Need to determine lastId to set the direction properly
        Route route = vehicle!.Route.Instructions;
        int currentId = route.GetID(newCurrent);
        int destId = route.GetID(newDestination);
        int lastId;

        if (route.Cyclic)
            lastId = currentId > 0 ? currentId - 1 : route.Cities.Length - 1;
        else if (destId > currentId) // non-cyclic, going forward
            lastId = currentId > 0 ? currentId - 1 : 1;
        else // non-cyclic, going back
            lastId = currentId == route.Cities.Length - 1 ? route.Cities.Length - 2 : currentId + 1;

        // set new params
        vehicle.Route.SetCurrent(currentId, lastId);
        vehicle.Route.SetPrivateField("state", vehicleState);

        destId = vehicle.Route.CallPrivateMethod<int>("GetNext", []);
        Log.Write($"Success. Changed {current.Name}/{destination.Name} => {currentId}.{newCurrent.Name}/{destId}.{route.Cities[destId].Name}");

        return false;
    }


    /// <summary>
    /// Finds the best replacement for a city in a new route.
    /// </summary>
    /// <param name="city"></param>
    /// <param name="route"></param>
    /// <param name="ports"></param>
    /// <param name="exclude"></param>
    /// <returns></returns>
    internal static CityUser? FindClosest(this Route route, CityUser city, bool ports, CityUser? exclude = null)
    {
        // Easy case - check if city is on the route
        if (route.Contains(city))
            return city;

        // Find closest city 
        return route.Cities
            .Where(c => c != exclude && (!ports || c.Sea != null))
            .MinBy(c => GameScene.GetDistance(c, city));
    }
}

#pragma warning restore CA1416 // Validate platform compatibility
