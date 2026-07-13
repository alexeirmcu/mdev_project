namespace SmartTripPlanner.Domain.Ports;

public interface IPromptTemplateProvider
{
    PromptTemplate GetTemplate(string name);
}
