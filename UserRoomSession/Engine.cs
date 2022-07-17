internal static class Engine
{
    public static void Solve(int numberOfRoomsOrSessions, int numberOfUsers)
    {
        var start = DateTime.Now;

        var model = new CpModel();

        // Build up the model:

        var sameRoomConstraints = new List<BoolVar>();

        var users = Enumerable.Range(1, numberOfUsers)
            .Select(CreateUser)
            .ToArray();

        foreach (var user in users)
        {
            // User must be in different room for every session
            model.AddAllDifferent(user.RoomBySession);
        }

        foreach (var pair in users.GetAllPairs())
        {
            // Get all pairs of users and add boolean vars for every session that is true if both are in the same room for that session.
            var isInSameRoom = pair.Item1.RoomBySession
                .Zip(pair.Item2.RoomBySession)
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

        Console.WriteLine("Number of rooms: {0}", numberOfRoomsOrSessions);
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

        Console.WriteLine("Session   | " + string.Join(" | ", Enumerable.Range(1, numberOfRoomsOrSessions).Select(i => i.ToString("000"))));
        Console.WriteLine("----------|-" + string.Join("-|-", Enumerable.Range(1, numberOfRoomsOrSessions).Select(_ => "---")));

        foreach (var user in users)
        {
            Console.WriteLine("{0,-10}| {1}", "User" + user.Id, string.Join(" | ", user.RoomBySession.Select(var => solver.Value(var).ToString("000"))));
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
            var roomBySession = Enumerable.Range(1, numberOfRoomsOrSessions)
                .Select(sessionId => model.NewIntVar(1, numberOfRoomsOrSessions, $"User {userId} Session {sessionId}")).ToArray();

            return new User(userId, roomBySession);
        }

        BoolVar AddIsInSameRoomConstraint((IntVar First, IntVar Second) sessionPair)
        {
            var first = sessionPair.First;
            var second = sessionPair.Second;

            var isInSameRoom = model.NewBoolVar(first.Name() + " and " + second.Name());

            model.Add(first == second).OnlyEnforceIf(isInSameRoom);
            model.Add(first != second).OnlyEnforceIf(isInSameRoom.Not());

            return isInSameRoom;
        }
    }

    private record User(int Id, IntVar[] RoomBySession);

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
