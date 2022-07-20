if (!int.TryParse(args.FirstOrDefault(), out var numberOfRoomsOrTimeSlots) || !int.TryParse(args.Skip(1).FirstOrDefault(), out var numberOfUsers))
{
    Console.WriteLine("Usage: UserRoomSession.exe <number of rooms> <number of users> [<timeout d.hh:mm>]");
    return;
}

if (!TimeSpan.TryParse(args.Skip(2).FirstOrDefault(), out var timeout))
{
    timeout = TimeSpan.FromMilliseconds(-1);
}

Engine.Solve(numberOfRoomsOrTimeSlots, numberOfUsers, timeout);


