using System.ServiceModel;
using FacturAR.Contracts.Soap;

namespace FacturAR.Api.Soap;

[ServiceContract(Namespace = FecaeNamespaces.Fev1)]
public interface IContractFecae
{
    [OperationContract(Action = FecaeNamespaces.Fev1 + "FECAESolicitar")]
    Task<FECAESolicitarResponse> FECAESolicitar(FECAESolicitarRequest request);

    [OperationContract(Action = FecaeNamespaces.Fev1 + "FECAEGenerar")]
    Task<FECAEGenerarResponse> FECAEGenerar(FECAEGenerarRequest request);
}
