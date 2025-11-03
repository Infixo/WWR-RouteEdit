#pragma warning disable CA1416 // Validate platform compatibility

using HarmonyLib;
using STM.GameWorld;
using STM.GameWorld.Commands;
using STM.GameWorld.Users;
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
        // 2025-11-03 Possible crash when it's been sold in the meantime
        if (__instance.Vehicle == null || __instance.Vehicle.Destroyed)
        {
            Log.Write("Warning! Vehicle is already sold or destroyed! Skipping.");
            return false;
        }

        // Command fields
        VehicleBaseUser vehicle = __instance.Vehicle;
        NewRouteSettings settings = __instance.GetPrivateField<NewRouteSettings>("settings");

        // Store reference cities and params
        byte vehicleState = vehicle.Route.Loading ? (byte)1 : (byte)0;
        decimal progress = vehicle.Route.GetPrivateField<decimal>("progress");
        int distance = vehicle.Route.Distance; // this will be used to calculate refunds
        CityUser current = vehicle.Route.Current;
        CityUser destination = vehicle.Route.Destination;

        Hub hub = __instance.Hub_city.GetHub(__instance.Company);
        Company company = scene.Session.Companies[__instance.Company];
        company.Line_manager.RemoveVehicle(__instance.Vehicle);

        if (vehicle == null || hub == null || vehicle.Destroyed || vehicle.Company != __instance.Company)
        {
            Log.Write($"Warning! Logic issue in command apply! v={vehicle} h={hub} d={vehicle?.Destroyed} c={vehicle?.Company}");
            return false;
        }

        long _import = __instance.Vehicle.GetImportCost(scene.Cities[hub.City].User, scene);
        if (_import > 0)
            vehicle.GetCompany(scene).AddExpense(_import, vehicle);

        __instance.Vehicle.Route.Destroy();
        //Route = new RouteInstance(new Route(settings), __instance.Vehicle, scene);
        vehicle.SetPublicProperty("Route", new RouteInstance(new Route(settings), vehicle, scene)); // this attaches a new route instance to required cities...
        // ...and also "registers" a vehicle in the first city...
        //vehicle.Route.Current.Vehicles.Remove(vehicle); // ...so it needs to be removed from there
        vehicle.Route.RemoveFromCity();

        // Move to a new hub if changed
        if (__instance.Vehicle.Hub != hub)
        {
            vehicle.Hub.Vehicles.Remove(vehicle);
            vehicle.SetPublicProperty("Hub", hub);
            vehicle.Hub.Vehicles.Add(vehicle);
        }

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
            vehicle!.Route.AddToCity();
            return false;
        }
        if (newCurrent != current && WorldwideRushExtensions.GetDistance(newCurrent, current, vehicle!.Entity_base.Type_name) == 0)
        {
            Log.Write($"There is no road/rails/sea path between {current.Name} and {newCurrent.Name}.");
            vehicle!.Route.AddToCity();
            return false;
        }

        CityUser? newDestination = vehicle?.Route.Instructions.FindClosest(destination, vehicle is ShipUser, newCurrent);
        if (newDestination == null)
        {
            Log.Write($"No replacement for the destination city {current.Name}.");
            vehicle!.Route.AddToCity();
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
        if (vehicle.Route.Loading) vehicle.Route.AddToCity(); // this makes sure that vehicle is IN the city to continue loading
        vehicle.Route.CallPrivateMethodVoid("GetNextDistance", [scene]);
        if (vehicle.Route.Moving)
        {
            if ((decimal)vehicle.Route.Distance < progress) progress = (decimal)(vehicle.Route.Distance - 1); // just for safety
            vehicle.Route.SetPrivateField("progress", progress);
            if (vehicle is RoadVehicleUser || vehicle is TrainUser)
                vehicle.GetPrivateField<RoadRoute>("route").Move(progress); // this puts the vehicle at the same distance it travelled previously
            else if (vehicle is ShipUser)
                vehicle.GetPrivateField<SeaRoute>("route").Move(progress);
        }

        destId = vehicle.Route.CallPrivateMethod<int>("GetNext", []);
        Log.Write($"Success: {current.Name}/{destination.Name} => {currentId}.{newCurrent.Name}/{destId}.{route.Cities[destId].Name}");

        // Passengers clean-up
        VehiclePassengers passengers = vehicle.Passengers;
        if (vehicle.Route.Loading) // when loading, just remove
        {
            for (int i = passengers.Items.Count - 1; i >= 0; i--)
                if (!vehicle.Route.Instructions.Contains(passengers.Items[i].Next.User))
                    passengers.RemovePassengers(i);
        }
        else // when moving, remove and refund
        {
            for (int i = passengers.Items.Count - 1; i >= 0; i--)
                if (!vehicle.Route.Instructions.Contains(passengers.Items[i].Next.User))
                    passengers.RemoveAndRefund(i, vehicle, distance, company);
        }

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


    public static void RemoveAndRefund(this VehiclePassengers passengers, int id, VehicleBaseUser vehicle, int distance, Company company)
    {
        Log.Write($"...removing {passengers.Items[id].People} passengers using {passengers.Items[id].Next.User.Name}");
        decimal _price = passengers.Items[id].demand_price * (decimal)passengers.Items[id].People;
        long nextTripPrice = (long)(_price * (decimal)vehicle.Entity_base.Passenger_pay_per_km * (decimal)distance);
        company.AddExpense(-nextTripPrice, vehicle);
        passengers.RemovePassengers(id);
    }
}

#pragma warning restore CA1416 // Validate platform compatibility
