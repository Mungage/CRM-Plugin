using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;

namespace SecondPlugin
{
    public class SecondPlugin : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            ITracingService tracingService = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));

            if (context.InputParameters.Contains("Target") && context.InputParameters["Target"] is Entity)
            {

                Entity executingEntity = (Entity)context.InputParameters["Target"];

                IOrganizationServiceFactory serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
                IOrganizationService service = serviceFactory.CreateOrganizationService(context.UserId);

                if (executingEntity.LogicalName != "new_assignment")
                    return;

                try
                {
                    // Retrieve a reference of the parent project
                    Entity retrievedAssignment = service.Retrieve("new_assignment", executingEntity.Id, new ColumnSet(true));
                    EntityReference parentRef = (EntityReference)retrievedAssignment["new_projectassignmentid"];
                    Entity parentEnt = new Entity();
                    parentEnt.LogicalName = parentRef.LogicalName;
                    parentEnt.Id = parentRef.Id;

                    // Using the parent project ID we retrieved we perform a query to find all the child assignments.
                    QueryExpression query = new QueryExpression("new_assignment");
                    query.Criteria.AddCondition(new ConditionExpression("new_projectassignmentid", ConditionOperator.Equal, parentEnt.Id));
                    query.ColumnSet = new ColumnSet(true);
                    var results = service.RetrieveMultiple(query);

                    // We turn the resulting EntityCollection into an enumerable DataCollection.
                    DataCollection<Entity> assignments = results.Entities;
                    int totalNoOfHours = 0;
                    decimal totalInvoiceValue = 0;

                    if(results.Entities.Any())
                    {
                        foreach (Entity entity in assignments)
                        {
                            if (entity.Contains("new_sumofhours") && entity.Contains("new_totalassignmentvalue"))
                            {
                                // Get the total number of hours of all related assignments
                                totalNoOfHours += Convert.ToInt32(entity.Attributes["new_sumofhours"]);

                                // Get the total value of all related assignments
                                Money m = (Money)entity.Attributes["new_totalassignmentvalue"];
                                totalInvoiceValue += m.Value;
                            } 

                        }
                        tracingService.Trace("SecondPlugin: Updating the project's fields");
                        parentEnt["new_totalnoofhours"] = totalNoOfHours;
                        parentEnt["new_totalinvoicevalue"] = new Money(totalInvoiceValue);
                        service.Update(parentEnt);
                    }
                }
                catch (FaultException<OrganizationServiceFault> ex)
                {
                    throw new InvalidPluginExecutionException("An error occurred in the SecondPlugin.", ex);
                }
                catch (Exception ex)
                {
                    tracingService.Trace("SecondPlugin: {0}", ex.ToString());
                    throw;
                }
            }
        }
    }
}
