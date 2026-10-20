import Keycloak from 'keycloak-js';

const keycloak = new Keycloak({
  url: 'http://localhost:8180',
  realm: 'halsingelive',
  clientId: 'admin-web',
});

export default keycloak;