internal static class Engine
{
    public static void Solve(int numberOfRoomsOrTimeSlots, int numberOfUsers)
    {
        var start = DateTime.Now;
        var numberOfRooms = numberOfRoomsOrTimeSlots;
        var numberOfTimeSlots = numberOfRoomsOrTimeSlots;

        var model = new CpModel();

        // Build up the model:

        var sameRoomConstraints = new List<BoolVar>();

        var users = Enumerable.Range(1, numberOfUsers)
            .Select(CreateUser)
            .ToArray();

        foreach (var user in users)
        {
            // User must be in different room for every time slot
            model.AddAllDifferent(user.TimeSlots.Select(timeSlot => timeSlot.Room));
        }

        foreach (var pair in users.GetAllPairs())
        {
            // Get all pairs of users and add boolean vars for every timeSlot that is true if both are in the same room for that time slot.
            var isInSameRoom = pair.Item1.TimeSlots.Select(timeSlot => timeSlot.Room)
                .Zip(pair.Item2.TimeSlots.Select(timeSlot => timeSlot.Room))
                .Select(AddIsInSameRoomConstraint).ToArray();

            sameRoomConstraints.AddRange(isInSameRoom);

            // Two users should be in the same room exactly once
            model.AddExactlyOne(isInSameRoom);
        }

        // Solve the model:

        var solver = new CpSolver
        {
            // StringParameters = "enumerate_all_solutions:true"
        };

        Console.WriteLine("Number of rooms: {0}", numberOfRooms);
        Console.WriteLine("Number of users: {0}", numberOfUsers);
        Console.WriteLine("Same room constraints: {0}", sameRoomConstraints.Count);

        using Callback? solutionCallback = null; // new();

        var status = solver.Solve(model, solutionCallback);

        // Output results:

        Console.WriteLine(status);
        Console.WriteLine("Elapsed: {0} min", (DateTime.Now - start).TotalMinutes);
        Console.WriteLine("Solutions: {0}", solutionCallback?.NumberOfSolutions);
        Console.WriteLine();

        if (status is not (CpSolverStatus.Optimal or CpSolverStatus.Feasible))
        {
            Console.WriteLine("No solution found.");
            return;
        }

        Console.WriteLine("TimeSlot  | " + string.Join(" | ", Enumerable.Range(1, numberOfTimeSlots).Select(i => i.ToString(" 00"))));
        Console.WriteLine("----------|-" + string.Join("-|-", Enumerable.Range(1, numberOfTimeSlots).Select(_ => "---")));

        foreach (var user in users)
        {
            Console.WriteLine("{0,-10}| {1}", "User" + user.Id, string.Join(" | ", user.TimeSlots.Select(timeSlot => "R" + solver.Value(timeSlot.Room).ToString("00"))));
        }

        Console.WriteLine();

        Console.WriteLine("In same room:");
        foreach (var item in sameRoomConstraints.Where(item => solver.BooleanValue(item))
                     .Select(item => item.Name()).Take(20))
        {
            Console.WriteLine(item);
        }

        User CreateUser(int userId)
        {
            var timeSlots = Enumerable.Range(1, numberOfTimeSlots)
                .Select(timeSlotId => new TimeSlot(timeSlotId, model.NewIntVar(1, numberOfRooms, $"User {userId} TimeSlot {timeSlotId}")))
                .ToArray();

            return new User(userId, timeSlots);
        }

        BoolVar AddIsInSameRoomConstraint((IntVar First, IntVar Second) timeSlotPair)
        {
            var first = timeSlotPair.First;
            var second = timeSlotPair.Second;

            var isInSameRoom = model.NewBoolVar(first.Name() + " and " + second.Name());

            model.Add(first == second).OnlyEnforceIf(isInSameRoom);
            model.Add(first != second).OnlyEnforceIf(isInSameRoom.Not());

            return isInSameRoom;
        }
    }

    private record User(int Id, TimeSlot[] TimeSlots);

    private record TimeSlot(int Id, IntVar Room);

    private class Callback : SolutionCallback
    {
        public int NumberOfSolutions { get; private set; }

        public override void OnSolutionCallback()
        {
            NumberOfSolutions++;
            // Console.WriteLine("Booleans: {0}", NumBooleans());
        }
    }
}
