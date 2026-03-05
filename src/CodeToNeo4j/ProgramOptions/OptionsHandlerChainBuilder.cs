namespace CodeToNeo4j.ProgramOptions;

public static class OptionsHandlerChainBuilder
{
    public static IOptionsHandler BuildChain(string[] allSupportedExtensions)
    {
        var purgeConfirmation = new PurgeConfirmationHandler();
        var msBuildRegistration = new MsBuildRegistrationHandler();
        var environmentSetup = new EnvironmentSetupHandler();
        var purgeExecution = new PurgeExecutionHandler(allSupportedExtensions);
        var solutionProcessing = new SolutionProcessingHandler();

        purgeConfirmation.SetNext(msBuildRegistration);
        msBuildRegistration.SetNext(environmentSetup);
        environmentSetup.SetNext(purgeExecution);
        purgeExecution.SetNext(solutionProcessing);

        return purgeConfirmation;
    }
}
